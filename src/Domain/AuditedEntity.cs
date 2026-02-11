namespace Domain;

public abstract class AuditedEntity : Entity
{
    [NotAudited]
    public DateTimeOffset CreatedAt { get; set; }

    [NotAudited]
    public DateTimeOffset? UpdatedAt { get; set; }
}