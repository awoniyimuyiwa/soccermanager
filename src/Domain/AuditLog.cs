
namespace Domain;

/// <summary>
/// Should be used to log all changes to the system, including user actions and system events. 
/// For tracking who did what and when, which is essential for auditing, debugging, and security purposes.
/// Neither this nor its navigation properties should extend <see cref="AuditedEntity"/> as their changes should not be audited to avoid infinite loop.
/// </summary>
public class AuditLog : Entity
{
    public string? BrowserInfo { get; set; }

    public double Duration { get; set; }

    public string? Exception { get; set; }

    public string? HttpMethod { get; set; }

    public string? IpAddress { get; set; }

    public string? RequestId { get; set; }

    public int StatusCode { get; set; }

    /// <summary>
    /// Time of change
    /// </summary>
    public DateTimeOffset TimeStamp { get; set; }

    public string? Url { get; set; }

    /// <summary>
    /// Id of the user the log was created for
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// User the log was created for
    /// </summary>
    /// <remarks>
    /// Set <see cref="UserId"/> only. Don't set <see cref="User"/> 
    /// to avoid an issue where <see cref="User"/> is loaded 
    /// from a different db context instance from the one used to save the audit log to DB,
    /// there by causing EF to think it's a new user entity and try to insert it again.
    /// </remarks>
    public ApplicationUser User { get;  protected set; } = null!;

    public List<AuditLogAction> AuditLogActions { get; set; } = [];

    public List<EntityChange> EntityChanges { get; set; } = [];
}

