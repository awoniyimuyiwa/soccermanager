namespace Domain;

public class EntityChange
{
    public long Id { get; protected set; } = default!;

    public long AuditLogId { get; set; } = default!;

    public string EntityName { get; set; } = default!;

    public long EntityId { get; set; } = default!;

    public string? NewValues { get; set; }

    public string? OldValues { get; set; }

    public EntityChangeType Type { get; set; } = default!;
}

/// <summary>
/// Type of change
/// </summary>
public enum EntityChangeType
{
    /// <summary>
    /// Created
    /// </summary>
    Created,

    /// <summary>
    /// Updated
    /// </summary>
    Updated,

    /// <summary>
    /// Delete
    /// </summary>
    Deleted
}


