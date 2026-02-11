using Domain;
using EntityFrameworkCore.Interceptors;
using EntityFrameworkCore.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
            var connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            options.UseSqlServer(connectionString)
            .AddInterceptors(serviceProvider.GetRequiredService<CustomSaveChangesInterceptor>());
            
            options.UseSeeding((context, _) =>
            {
                var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

                Seed(
                    configuration,
                    userManager,
                    roleManager);
            });

            options.UseAsyncSeeding(async (context, _, cancellationToken) =>
            {
                var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

                await SeedAsync(
                    configuration,
                    userManager,
                    roleManager,
                    cancellationToken);
            });
        });

        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ITransferRepository, TransferRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    private static void Seed(
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        var adminEmail = configuration["AdminUser:Email"];
        var adminUserName = configuration["AdminUser:UserName"];
        var adminPassword = configuration["AdminUser:Password"];
        var adminRole = Domain.Constants.AdminRoleName;

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var roleExists = roleManager.Roles.Any(r => r.Name == adminRole);
            if (!roleExists)
            {
                roleManager.CreateAsync(new ApplicationRole
                {
                    ExternalId = Guid.NewGuid(),
                    Name = adminRole,
                    NormalizedName = adminRole.ToUpperInvariant()
                }).GetAwaiter().GetResult();
            }

            var existing = userManager.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();
            if (existing == null)
            {
                var adminUser = new ApplicationUser
                {
                    ExternalId = Guid.NewGuid(),
                    UserName = adminUserName,
                    NormalizedUserName = adminUserName!.ToUpperInvariant(),
                    Email = adminEmail,
                    NormalizedEmail = adminEmail.ToUpperInvariant(),
                    EmailConfirmed = true
                };
                var result = userManager.CreateAsync(adminUser, adminPassword).GetAwaiter().GetResult();
                if (result.Succeeded)
                {
                    userManager.AddToRoleAsync(adminUser, adminRole).GetAwaiter().GetResult();
                }
            }
            else
            {
                var isInRole = userManager.IsInRoleAsync(existing, adminRole).GetAwaiter().GetResult();
                if (!isInRole)
                {
                    userManager.AddToRoleAsync(existing, adminRole).GetAwaiter().GetResult();
                }
            }
        }
    }

    private static async Task SeedAsync(
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        CancellationToken cancellationToken = default)
    {
        var adminEmail = configuration["AdminUser:Email"];
        var adminUserName = configuration["AdminUser:UserName"];
        var adminPassword = configuration["AdminUser:Password"];
        var adminRole = Domain.Constants.AdminRoleName;

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var roleExists = roleManager.Roles.Any(r => r.Name == adminRole);
            if (!roleExists)
            {
                await roleManager.CreateAsync(new ApplicationRole
                {
                    ExternalId = Guid.NewGuid(),
                    Name = adminRole,
                    NormalizedName = adminRole.ToUpperInvariant()
                });
            }

            var existing = await userManager.FindByEmailAsync(adminEmail);
            if (existing == null)
            {
                var adminUser = new ApplicationUser
                {
                    ExternalId = Guid.NewGuid(),
                    UserName = adminUserName,
                    NormalizedUserName = adminUserName!.ToUpperInvariant(),
                    Email = adminEmail,
                    NormalizedEmail = adminEmail.ToUpperInvariant(),
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, adminRole);
                }
            }
            else
            {
                var isInRole = await userManager.IsInRoleAsync(existing, adminRole);
                if (!isInRole)
                {
                    await userManager.AddToRoleAsync(existing, adminRole);
                }
            }
        }
    }
}
