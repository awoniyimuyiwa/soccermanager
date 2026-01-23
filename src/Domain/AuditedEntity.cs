namespace Domain;

public abstract class AuditedEntity : Entity
{
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}