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
/// Specifies the type of modification made to an entity.
/// </summary>
/// <remarks>
/// IMPORTANT: Always append new members to the end of the list to maintain 
/// database compatibility and prevent value shifts for existing records.
/// </remarks>
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


