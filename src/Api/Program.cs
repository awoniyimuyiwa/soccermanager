using Api;
using Api.Controllers.V1;
using Api.MiddleWares;
using Application.Extensions;
using Asp.Versioning;
using Domain;
using EntityFrameworkCore.Extensions;
using Scalar.AspNetCore;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);

// Add services to the container.

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

builder.Services.AddEntityFrameworkServices();

builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddRoles<ApplicationRole>()
    .AddCustomEntityFrameworkIdentityStores();

builder.Services.AddApplicationServices();

builder.Services.AddAuthorization();

var app = builder.Build();

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

app.UseMiddleware<TransactionMiddleware>();

app.MapGroup("v{version:apiVersion}/auth")
    .MapCustomIdentityApiV1<ApplicationUser>()
    .WithApiVersionSet(app.NewApiVersionSet().HasApiVersion(new ApiVersion(1, 0)).Build());

app.MapControllers();

app.Run();
