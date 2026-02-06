using Microsoft.AspNetCore.Identity;

namespace Domain;

public class ApplicationRole : IdentityRole<long>
{
    public Guid ExternalId { get; set; }
}

