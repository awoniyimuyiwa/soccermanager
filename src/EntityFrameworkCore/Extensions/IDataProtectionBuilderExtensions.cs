using Microsoft.AspNetCore.DataProtection;

namespace EntityFrameworkCore.Extensions;

public static class IDataProtectionBuilderExtensions
{
    public static IDataProtectionBuilder CustomPersistKeysToDbContext(this IDataProtectionBuilder builder)
    {
        builder.PersistKeysToDbContext<ApplicationDbContext>();

        return builder;
    }
}

