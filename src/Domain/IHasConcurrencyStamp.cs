namespace Domain;

public interface IHasConcurrencyStamp
{
    [NotAudited]
    public string? ConcurrencyStamp { get; set; }
}
