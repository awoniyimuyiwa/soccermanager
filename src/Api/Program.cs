using Api;
using Api.BackgroundServices;
using Api.Controllers.V1;
using Api.Extensions;
using Api.MiddleWares;
using Api.Options;
using Api.RateLimiting;
using Api.Utils;
using Application.Extensions;
using Asp.Versioning;
using Domain;
using EntityFrameworkCore.Extensions;
using Medallion.Threading;
using Medallion.Threading.SqlServer;
using Microsoft.AspNetCore.HttpOverrides;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using Scalar.AspNetCore;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

var builder = WebApplication.CreateBuilder(args);

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("Startup");

builder.Services.AddOptions<AuditLogOptions>()
    .Bind(builder.Configuration.GetSection(AuditLogOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RateLimitOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<CustomDataProtectionOptions>()
    .Bind(builder.Configuration.GetSection(CustomDataProtectionOptions.SectionName))
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

// Add services to the container.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddExceptionHandler<ExceptionHandler>();

builder.Services.AddProblemDetails(); // Enable Problem Details support

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options => options.AddSchemaTransformer((schema, context, cancellationToken) =>
{
    if (context.JsonTypeInfo.Type.IsEnum)
    {
        var enumType = context.JsonTypeInfo.Type;
        schema.Enum ??= [];
        schema.Enum.Clear();

        // Add enum names as JsonNodes (Standard for .NET 10 OpenAPI)
        foreach (var name in Enum.GetNames(enumType))
        {
            schema.Enum.Add(JsonValue.Create(name));
        }

        schema.Type = Microsoft.OpenApi.JsonSchemaType.String;
    }

    return Task.CompletedTask;
}));

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

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddKeyedSingleton(Domain.Constants.AuditLogJsonSerializationOptionsName, new JsonSerializerOptions
{
    TypeInfoResolver = new DefaultJsonTypeInfoResolver 
    { 
        Modifiers = { AuditLogJsonModifier.Modify } 
    },
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
});

var connectionMultiplexer = ConnectionMultiplexer.Connect(
    builder.Configuration.GetConnectionString("Redis") 
    ?? throw new InvalidOperationException("Connection string 'Redis' not found."));
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => connectionMultiplexer);

builder.Services.AddEntityFrameworkServices();

builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddRoles<ApplicationRole>()
    .AddCustomEntityFrameworkIdentityStores();

builder.Services.AddApplicationServices();

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

builder.Services.AddAuthorization();

builder.Services.AddSingleton<RateLimitService>();

builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    // Distributed Global Policy (Fixed Window). Ensures the ENTIRE cluster does not exceed global rate limit (e.g. 5000)
    rateLimiterOptions.AddPolicy<string, GlobalRateLimitPolicy>(Api.Constants.GlobalRateLimitPolicyName);

    // Distributed User Policy(Sliding Window)
    rateLimiterOptions.AddPolicy<string, UserRateLimitPolicy>(Api.Constants.UserRateLimitPolicyName);
});

builder.Services.AddCustomDataProtection(
    builder.Configuration, 
    connectionMultiplexer, 
    logger);

var app = builder.Build();

app.UseForwardedHeaders();

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
}
else
{
    app.UseExceptionHandler(); // Use the registered IExceptionHandler in non dev environments
    app.UseHsts();
}

app.UseHttpsRedirection();

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

app.MapGroup("v{version:apiVersion}/auth")
    .MapCustomIdentityApiV1<ApplicationUser>()
    .WithApiVersionSet(app.NewApiVersionSet().HasApiVersion(new ApiVersion(1, 0)).Build())
    .RequireRateLimiting(Api.Constants.GlobalRateLimitPolicyName)
    .RequireRateLimiting(Api.Constants.UserRateLimitPolicyName);
//.WithMetadata(new AuditedAttribute()); // Uncomment to audit identity endpoints

app.MapControllers()
    .RequireRateLimiting(Api.Constants.GlobalRateLimitPolicyName)
    .RequireRateLimiting(Api.Constants.UserRateLimitPolicyName);

app.Run();
