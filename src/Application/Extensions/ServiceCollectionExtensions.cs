using Application.Attributes;
using Application.Contracts;
using Application.Services;
using Domain;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register application services here
        services
            .AddScoped<IPlayerService, PlayerService>()
            .AddScoped<ITeamService, TeamService>()
            .AddScoped<ITransferService, TransferService>()
            .AddScoped<IUserService, UserService>()
            .AddBackgroundJobHandlers()
            .AddTransient<IBackgroundJobRunner, BackgroundJobRunner>();
           
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
        var handlers = Assembly.GetExecutingAssembly().GetTypes()
            .Where(h => h.IsClass && !h.IsAbstract && typeof(IBackgroundJobHandler).IsAssignableFrom(h))
            .Select(h => new
            {
                Attr = h.GetCustomAttribute<BackgroundJobHandlerAttribute>(),
                Type = h
            })
            .Where(h => h.Attr != null);

        foreach (var handler in handlers)
        {
            // Register using the Enum from the attribute as the key to allow dynamic resolution by the runner.
            services.AddKeyedScoped(typeof(IBackgroundJobHandler), handler.Attr!.Type, handler.Type);
        }

        return services;
    }
}
