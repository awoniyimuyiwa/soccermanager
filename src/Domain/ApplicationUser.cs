using Microsoft.AspNetCore.Identity;
namespace Domain;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";
}

