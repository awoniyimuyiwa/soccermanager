namespace Domain;

public class AuditLog : Entity
{
    public string EntityName { get; set; } = "";

    public Guid? EntityId { get; set; }

    /// <summary>
    /// Single JSON diff payload including old/new values
    /// </summary>
    public string JsonDiff { get; set; } = "";

    /// <summary>
    /// Time of change
    /// </summary>
    public DateTimeOffset TimeStamp { get; set; }

    /// <summary>
    /// Type of audit log
    /// </summary>
    public AuditLogType Type { get; set; }

    /// <summary>
    /// Id of the user the log was created for
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User the log was created for
    /// </summary>
    public ApplicationUser User { get; set; } = null!;
}

/// <summary>
/// The type of audit log
/// </summary>
public enum AuditLogType
{
    /// <summary>
    /// Create
    /// </summary>
    Create,

    /// <summary>
    /// Update
    /// </summary>
    Update,

    /// <summary>
    /// Delete
    /// </summary>
    Delete
}
