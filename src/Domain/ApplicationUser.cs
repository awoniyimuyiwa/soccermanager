using Microsoft.AspNetCore.Identity;

namespace Domain;

public class ApplicationUser : IdentityUser<long>
{
    public Guid ExternalId { get; set; }
    
    public string FirstName { get; set; } = "";

    public string LastName { get; set; } = "";
}

