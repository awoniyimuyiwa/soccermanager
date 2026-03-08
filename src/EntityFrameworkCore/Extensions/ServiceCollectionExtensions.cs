using Domain;
using EntityFrameworkCore.Interceptors;
using EntityFrameworkCore.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.Extensions;
 
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEntityFrameworkServices(this IServiceCollection services)
    {
        services.AddScoped<IAuditLogManager, AuditLogManager>();
        services.AddScoped<CustomSaveChangesInterceptor>();
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            options.Configure(configuration)
            .AddInterceptors(serviceProvider.GetRequiredService<CustomSaveChangesInterceptor>());
        });

        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IBackgroundServiceStatRepository, BackgroundServiceStatRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ITransferRepository, TransferRepository>();
        services.AddScoped<IUserRepository, UserRepository>();  
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
