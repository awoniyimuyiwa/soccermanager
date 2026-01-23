namespace Domain;

public interface IHasConcurrencyStamp
{
    public string? ConcurrencyStamp { get; set; }
}
