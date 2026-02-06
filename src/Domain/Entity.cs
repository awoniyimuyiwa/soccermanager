namespace Domain;

public abstract class Entity
{
    public long Id { get; private set; } = 0;

    public Guid ExternalId { get; set; } = Guid.NewGuid();
}
