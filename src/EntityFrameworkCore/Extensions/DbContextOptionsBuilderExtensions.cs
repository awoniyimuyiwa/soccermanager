using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EntityFrameworkCore.Extensions;

public static class DbContextOptionsBuilderExtensions
{
    public static TBuilder Configure<TBuilder>(
        this TBuilder options,
        IConfiguration configuration) where TBuilder : DbContextOptionsBuilder
    {
        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));

        // For EF Tools / CLI (Sync)
        options.UseSeeding((context, _) =>
        {
            var logger = context.GetService<ILoggerFactory>()?.CreateLogger("DatabaseSeeding");

            try
            {
                logger?.LogInformation("Database seeding process started.");
                Task.Run(() => DbInitializer.Seed(context)).GetAwaiter().GetResult();
                logger?.LogInformation("Database seeding process completed.");
            }
            catch (Exception ex)
            {
                logger?.LogCritical(ex, "Seeding failed (Sync)");
                throw;
            }
        });

        // For App Startup (Async) - Reuses the same logic
        options.UseAsyncSeeding(async (context, _, ct) =>
        {
            var logger = context.GetService<ILoggerFactory>()?.CreateLogger("DatabaseSeeding");
            try
            {
                logger?.LogInformation("Database seeding process started.");
                await DbInitializer.Seed(context, ct);
                logger?.LogInformation("Database seeding process completed.");
            }
            catch (Exception ex)
            {
                logger?.LogCritical(ex, "Seeding failed (Async)");
                throw;
            }
        });

        return options;
    }
}

