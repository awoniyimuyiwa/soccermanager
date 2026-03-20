namespace Domain;

public interface IHasConcurrencyStamp
{
    [NotAudited]
    public string? ConcurrencyStamp { get; set; }
}

public interface IHasInternalId
{
    long InternalId { get; }

    DateTimeOffset CreatedAt { get; }
}
