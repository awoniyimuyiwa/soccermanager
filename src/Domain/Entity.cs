namespace Domain;

public abstract class Entity
{
    public long Id { get; private set; }

    public Guid ExternalId { get; set; }
}
