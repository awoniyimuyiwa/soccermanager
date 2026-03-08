using Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore;

static class DbInitializer
{
    public static async Task Seed(
        DbContext applicationDbContext,
        CancellationToken cancellationToken = default)
    {
        // EF Core 9+ manages the transaction automatically during migrations and seeding.
        var serviceProvider = applicationDbContext.GetService<IServiceProvider>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        await SeedAdminUser(
            configuration,
            userManager,
            roleManager);
    }

    private static async Task SeedAdminUser(
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
                await roleManager.CreateAsync(new ApplicationRole
                {
                    ExternalId = Guid.NewGuid(),
                    Name = adminRole,
                    NormalizedName = adminRole.ToUpperInvariant()
                });
            }

            var existing = await userManager.FindByEmailAsync(adminEmail);
            if (existing is null)
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