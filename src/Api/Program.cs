using Api;
using Api.BackgroundServices;
using Api.Controllers.V1;
using Api.MiddleWares;
using Api.Options;
using Application.Extensions;
using Asp.Versioning;
using Domain;
using EntityFrameworkCore.Extensions;
using Medallion.Threading;
using Medallion.Threading.SqlServer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Clear the default (which only allows loopback)
    //options.KnownProxies.Clear();

    // Add the IP of Load Balancer or Nginx server
    // options.KnownProxies.Add(IPAddress.Parse("10.0.0.100"));
});

builder.Services.Configure<AuditLogOptions>(builder.Configuration.GetSection("AuditLogOptions"));

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
    // Choose your versioning strategy (e.g., URL segment)
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

builder.Services.AddEntityFrameworkServices();

builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddRoles<ApplicationRole>()
    .AddCustomEntityFrameworkIdentityStores();

builder.Services.AddApplicationServices();

builder.Services.AddSingleton<IDistributedLockProvider>((serviceProvider) =>
{
    // Configure distributed lock rovider here, SQL server, Postgresql or Redis. Zookeeper requires ZooKeeperNetEx library
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    return new SqlDistributedSynchronizationProvider(connectionString);
});

builder.Services.AddSingleton<AuditLogCleanupTrigger>();
builder.Services.AddSingleton<IAuditLogCleanupTrigger>(sp => sp.GetRequiredService<AuditLogCleanupTrigger>());
builder.Services.AddSingleton<IAuditLogCleanupReader>(sp => sp.GetRequiredService<AuditLogCleanupTrigger>());
builder.Services.AddHostedService<AuditLogCleanupService>();

builder.Services.AddAuthorization();

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

app.UseAuthorization();

app.UseMiddleware<AuditLogMiddleware>() // AuditMiddleware should be as early as possible to capture all necessary information.
    .UseMiddleware<TransactionMiddleware>();

app.MapGroup("v{version:apiVersion}/auth")
    .MapCustomIdentityApiV1<ApplicationUser>()
    .WithApiVersionSet(app.NewApiVersionSet().HasApiVersion(new ApiVersion(1, 0)).Build());
    //.WithMetadata(new AuditedAttribute()); // Uncomment to audit identity endpoints

app.MapControllers();

app.Run();
