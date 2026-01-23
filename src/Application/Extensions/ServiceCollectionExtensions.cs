using Application.Contracts;
using Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        // Register application services here
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<ITransferService, TransferService>();
        services.AddScoped<IPlayerService, PlayerService>();
    }
}
