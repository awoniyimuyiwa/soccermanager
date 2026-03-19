using Application.Attributes;
using Application.BackgroundJobs;
using Application.BackgroundJobs.Handlers;
using Application.Contracts;
using Application.Contracts.BackgroundJobs;
using Application.Services;
using Domain;
using Domain.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register application services here
        services
            .AddScoped<IBackgroundJobManager, BackgroundJobManager>()
            .AddScoped<IPlayerService, PlayerService>()
            .AddScoped<ITeamService, TeamService>()
            .AddScoped<ITransferService, TransferService>()
            .AddScoped<IUserService, UserService>()
            .AddBackgroundJobHandlers();
           
        return services;
    }

    /// <summary>
    /// Scans the assembly for all concrete <see cref="IBackgroundJobHandler"/> implementations 
    /// decorated with <see cref="BackgroundJobHandlerAttribute"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the handlers to.</param>
    /// <remarks>
    /// Handlers are registered as Keyed Scoped services using their <see cref="BackgroundJobType"/> 
    /// as the service key. This ensures they participate in the same database context and 
    /// transaction as the <see cref="IUnitOfWork"/> during job execution.
    /// </remarks>
    private static IServiceCollection AddBackgroundJobHandlers(this IServiceCollection services)
    {
        var mappings = new Dictionary<Type, BackgroundJobType>();

        var handlers = Assembly.GetExecutingAssembly().GetTypes()
            .Where(h => h.IsClass && !h.IsAbstract && typeof(IBackgroundJobHandler).IsAssignableFrom(h))
            .Select(h => new
            {
                Attr = h.GetCustomAttribute<BackgroundJobHandlerAttribute>(),
                Implementation = h,
                // Find BackgroundJobHandler<T> and get the T
                DtoType = h.BaseType?.IsGenericType == true && h.BaseType.GetGenericTypeDefinition() == typeof(BackgroundJobHandler<>)
                    ? h.BaseType.GetGenericArguments()[0]
                    : null
            })
            .Where(h => h.Attr != null);

        foreach (var h in handlers)
        {
            services.AddKeyedScoped(typeof(IBackgroundJobHandler), h.Attr!.Type, h.Implementation);

            if (h.DtoType != null)
            {
                mappings[h.DtoType] = h.Attr.Type;
            }
        }

        services.AddTransient<IBackgroundJobRunner, BackgroundJobRunner>();

        services.AddSingleton<IBackgroundJobTypeRegistry>(new BackgroundJobTypeRegistry(mappings));

        return services;
    }
}
