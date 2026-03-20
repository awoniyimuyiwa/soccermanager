namespace Domain;

public interface IHasInternalCursor
{
    long InternalId { get; }

    DateTimeOffset CreatedAt { get; }
}
