using Api;
using Api.BackgroundServices;
using Api.Controllers.V1;
using Api.Extensions;
using Api.Filters;
using Api.MiddleWares;
using Api.OpenApi;
using Api.Options;
using Api.Services;
using Api.Utils;
using Application.Contracts;
using Application.Extensions;
using Asp.Versioning;
using Domain;
using EntityFrameworkCore.Extensions;
using MaxMind.GeoIP2;
using Medallion.Threading;
using Medallion.Threading.SqlServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using Scalar.AspNetCore;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using UAParser;

var builder = WebApplication.CreateBuilder(args);

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("Startup");

// Changing the app name value will invalidate existing cache entries, rate limiting keys,
// auth tickets and data protection keys.
var appName = builder.Configuration.GetValue<string>("AppName") 
    ?? throw new InvalidOperationException("Setting 'AppName' is missing from configuration.");

builder.Services.AddOptions<AuditLogOptions>()
    .Bind(builder.Configuration.GetSection(AuditLogOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<BackgroundJobOptions>()
    .BindConfiguration(BackgroundJobOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<CustomDataProtectionOptions>()
    .Bind(builder.Configuration.GetSection(CustomDataProtectionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<SecurityOptions>()
    .Bind(builder.Configuration.GetSection(SecurityOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Clear the default (which only allows loopback)
    //options.KnownProxies.Clear();

    // Add the IP of Load Balancer or Nginx server
    // options.KnownProxies.Add(IPAddress.Parse("10.0.0.100"));
});

// Post configure
builder.Services.AddSingleton<IPostConfigureOptions<BearerTokenOptions>, ConfigureBearerTokenOptions>();
builder.Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, ConfigureCookieAuthenticationOptions>();
builder.Services.AddSingleton<IPostConfigureOptions<JsonOptions>, ConfigureJsonOptions>();

// Add services to the container.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddExceptionHandler<ExceptionHandler>();

builder.Services.AddProblemDetails(); // Enable Problem Details support

builder.Services.AddControllers(options =>
{
    options.Filters.Add<AntiforgeryAuthorizationFilter>();
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = PascalCaseSplitterRegex().Replace(appName, "$1 $2");
        return Task.CompletedTask;
    });

    options.AddSchemaTransformer<EnumSchemaTransformer>();
    options.AddSchemaTransformer<PasswordSchemaTransformer>();

    options.AddOperationTransformer<AntiforgeryHeaderOperationTransformer>();
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    // Choose versioning strategy (e.g., URL segment)
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddMvc()
.AddApiExplorer(options =>
{
    // Configure the format of the version in the route URL
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
}); 

builder.Services.AddKeyedSingleton(Domain.Constants.AuditLogJsonSerializationOptionsName, new JsonSerializerOptions
{
    TypeInfoResolver = new DefaultJsonTypeInfoResolver 
    { 
        Modifiers = { AuditLogJsonModifier.Modify } 
    },
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
});

var cacheVersion = builder.Configuration.GetValue<string>("CacheOptions:Version") ?? "V1";
var cachePrefix = $"{appName}:{cacheVersion}:";

var connectionMultiplexer = ConnectionMultiplexer.Connect(
    builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Connection string 'Redis' not found."));
var prefixedMultiplexer = connectionMultiplexer.WithKeyPrefix(cachePrefix);
builder.Services.AddSingleton(prefixedMultiplexer);

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

builder.Services.AddStackExchangeRedisCache(options =>
{
    // Leave this empty! The Proxy already adds cachePrefix to all keys,
    // and adding it here will result in double prefixing 
    options.InstanceName = string.Empty;
    options.ConnectionMultiplexerFactory = () => Task.FromResult(prefixedMultiplexer);
});

builder.Services.AddDistributedMemoryCache();

builder.Services.AddEntityFrameworkServices();

builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddRoles<ApplicationRole>()
    .AddCustomEntityFrameworkStores();

builder.Services.AddApplicationServices();

builder.Services.AddHttpClient(Api.Constants.LlmHttpClientName, client =>
{
    // High enough for local models (Ollama), safe for Cloud
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddScoped<IChatClientFactory, ChatClientFactory>();

// Configure distributed lock provider here, SQL server, Postgresql or Redis. Zookeeper requires ZooKeeperNetEx library
builder.Services.AddSingleton<IDistributedLockProvider>(
    serviceProvider => new SqlDistributedSynchronizationProvider(builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")));

builder.Services.AddResiliencePipeline<string, IDistributedSynchronizationHandle?>(
    Api.Constants.DistributedLockResiliencePolicyName, (builder, context) =>
    {
        builder
            // Per-attempt timeout: Ensures a stuck network call doesn't hang the background service
            .AddTimeout(TimeSpan.FromSeconds(5))
            .AddRetry(new RetryStrategyOptions<IDistributedSynchronizationHandle?>
            {
                ShouldHandle = new PredicateBuilder<IDistributedSynchronizationHandle?>()
                    .Handle<TimeoutRejectedException>() // Matches the timeout above
                    .Handle<Exception>()
                    .HandleResult(result => result is null),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(10),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true // Prevent thundering herd
            });
    });

builder.Services.AddSingleton<AuditLogCleanupTrigger>();
builder.Services.AddSingleton<IAuditLogCleanupTrigger>(sp => sp.GetRequiredService<AuditLogCleanupTrigger>());
builder.Services.AddSingleton<IAuditLogCleanupReader>(sp => sp.GetRequiredService<AuditLogCleanupTrigger>());
builder.Services.AddHostedService<AuditLogCleanupService>();

builder.Services.AddSingleton<BackgroundJobTrigger>();
builder.Services.AddSingleton<IBackgroundJobTrigger>(sp => sp.GetRequiredService<BackgroundJobTrigger>());
builder.Services.AddSingleton<IBackgroundJobReader>(sp => sp.GetRequiredService<BackgroundJobTrigger>());
builder.Services.AddHostedService<BackgroundJobService>();

builder.Services.AddSingleton<RateLimitService>();
builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    // Distributed global policy (Cluster-wide protection)
    // Ensures the entire cluster does not exceed global rate limit (e.g. 5000)
    rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var rateLimitService = context.RequestServices.GetRequiredService<RateLimitService>();
        return rateLimitService.GetGlobalPolicyPartition(context);
    });

    // Distributed user Policy (Per-user/IP protection)
    rateLimiterOptions.AddPolicy(Api.Constants.UserRateLimitPolicyName, context =>
    {
        var rateLimitService = context.RequestServices.GetRequiredService<RateLimitService>();
        return rateLimitService.GetUserPolicyPartition(context);
    });

    // Unified rejection handling
    rateLimiterOptions.OnRejected = (context, token) =>
    {
        var rateLimitService = context.HttpContext.RequestServices.GetRequiredService<RateLimitService>();
        return rateLimitService.HandleOnRejected(context, token);
    };
});

// Add user agent parser
builder.Services.AddSingleton(_ => Parser.GetDefault());

// Add MaxMind DatabaseReader for geo location-based features
builder.Services.AddSingleton<IGeoIP2DatabaseReader>(sp =>
{
    var path = Path.Combine(
        builder.Environment.ContentRootPath,
        "Data",
        "GeoLite2-City.mmdb");

    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"GeoLite database not found at {path}. Ensure 'Copy to Output Directory' is configured.");
    }

    return new DatabaseReader(path);
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<RedisTicketStore>();
builder.Services.AddSingleton<ITicketStore>(sp => sp.GetRequiredService<RedisTicketStore>());
builder.Services.AddSingleton<ISecureDataFormat<AuthenticationTicket>>(sp => sp.GetRequiredService<RedisTicketStore>());
builder.Services.AddSingleton<IUserSessionManager>(sp => sp.GetRequiredService<RedisTicketStore>());

var customDataProtectionOptions =       
    builder.Configuration.GetRequiredSection(CustomDataProtectionOptions.SectionName)       
    .Get<CustomDataProtectionOptions>();

builder.Services.AddCustomDataProtection( 
    appName,
    customDataProtectionOptions!,
    prefixedMultiplexer, 
    logger);

// Register the specific base IDataProtector for your application
builder.Services.AddSingleton<IDataProtector>(sp =>
{
    var provider = sp.GetRequiredService<IDataProtectionProvider>();

    return provider.CreateProtector(cachePrefix);
});

builder.Services.AddAntiforgery(options =>
{
    // In modern browsers, the __Host- prefix enforces strict security: 
    // Must be HTTPS (Secure) 
    // Must have Path=/ 
    // Prevents subdomains from overriding or 'tossing' this cookie (Domain Locking).
    options.Cookie.Name = Api.Constants.AntiforgeryCookieName;

    options.Cookie.HttpOnly = true;
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    // The Antiforgery system will look for the request token in this header.
    // SPAs must include this header for state-changing requests (POST, PUT, PATCH, DELETE)
    // when using Cookie Authentication to prevent Cross-Site Request Forgery (CSRF).
    // The value should be read from the 'XSRF-TOKEN' cookie (provided by the /auth/antiforgery-token endpoint)
    // and attached to the request headers via JavaScript.
    options.HeaderName = Api.Constants.AntiforgeryHeaderName;
});

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseForwardedHeaders();

app.UseHttpsRedirection();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseDeveloperExceptionPage(); // Detailed page for development
    app.MapOpenApi();
    app.MapScalarApiReference();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");

    });

    app.MapGet("/", (context) =>
    {
        context.Response.Redirect("/swagger");
        return Task.CompletedTask;
    });
}
else
{
    app.UseExceptionHandler(); // Use the registered IExceptionHandler in non dev environments
    app.UseHsts();
}

// Security headers
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/scalar")
               && !context.Request.Path.StartsWithSegments("/swagger"),
    appBuilder =>
    {
        appBuilder.UseXContentTypeOptions();
        appBuilder.UseXfo(options => options.Deny());
        appBuilder.UseCsp(options => options
            .DefaultSources(s => s.None())
            .FrameAncestors(s => s.None())
        );
    }
);

/* 
 * CORS is currently disabled because the Frontend and API should ideally share the same domain.
 * Under the Same-Origin Policy (SOP), the browser allows the UI to read all 
 * 'X-RateLimit-*' and 'Retry-After' headers automatically.
 * 
 * If the Frontend is moved to a different domain (Cross-Origin), 
 * CORS must be enabled and headers MUST be explicitly exposed:
 * 
 * policy.WithExposedHeaders(
 *    "X-RateLimit-Limit", 
 *    "X-RateLimit-Remaining",
 *    "X-RateLimit-Reset",
 *    "X-RateLimit-Scope",
 *    "Retry-After");
 */
// app.UseCors(); // Not required for same-origin deployments
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseMiddleware<RateLimitHeadersMiddleware>() // Add rate limit headers to successful requests for UI frameworks
    .UseMiddleware<AuditLogMiddleware>() // Audit log middleware should be as early as possible to capture all necessary information.
    .UseMiddleware<TransactionMiddleware>();

app.MapGroup("v{version:apiVersion}")
    .AddEndpointFilter<AntiforgeryEndpointFilter>()
    .MapCustomIdentityApiV1<ApplicationUser>()
    .WithApiVersionSet(app.NewApiVersionSet().HasApiVersion(new ApiVersion(1, 0)).Build())
    .RequireRateLimiting(Api.Constants.UserRateLimitPolicyName);
    //.WithMetadata(new AuditedAttribute()); // Uncomment to audit identity endpoints

app.MapControllers()
    .RequireRateLimiting(Api.Constants.UserRateLimitPolicyName);

app.Run();

partial class Program
{
    [GeneratedRegex("([a-z])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex PascalCaseSplitterRegex();
}