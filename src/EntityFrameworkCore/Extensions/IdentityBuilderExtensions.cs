using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.Extensions;

public static class IdentityBuilderExtensions
{
    public static IdentityBuilder AddCustomEntityFrameworkIdentityStores(this IdentityBuilder builder)
    {
        builder.AddEntityFrameworkStores<ApplicationDbContext>();

        return builder;
    }
}
