using Domain;
using EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore;

class DbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Credentials (Connection String, Admin credentials for seeding) 
        // must be in User Secrets, not appsettings.json. 
        // This ensures EF tools only access what is necessary for schema/seed management.
        var configuration = new ConfigurationBuilder()
          .AddUserSecrets<DbContextFactory>()
          .Build();

        var services = new ServiceCollection();

        services.AddEntityFrameworkSqlServer();

        services.AddLogging(b => b.AddConsole());
        services.AddSingleton<IConfiguration>(configuration);
        services.AddIdentity<ApplicationUser, ApplicationRole>()    
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.Configure(configuration);

            // FORCE EF to use this specific service provider
            options.UseInternalServiceProvider(sp);
        });

        var serviceProvider = services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<ApplicationDbContext>();
    }
}
