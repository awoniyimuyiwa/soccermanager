namespace Domain;

public interface IHasCursorMetadata
{
    public long Id { get; } 

    [NotAudited]
    public DateTimeOffset CreatedAt { get; set; }
}
