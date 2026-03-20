namespace Domain;


public interface IAuditedEntity : IHasCursorMetadata
{
    [NotAudited]
    public DateTimeOffset? UpdatedAt { get; set; }
}

public abstract class AuditedEntity : Entity, IAuditedEntity
{
    [NotAudited]
    public DateTimeOffset CreatedAt { get; set; }

    [NotAudited]
    public DateTimeOffset? UpdatedAt { get; set; }
}