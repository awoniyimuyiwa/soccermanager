namespace Domain;

public record TeamDto(
    Guid Id,
    string? Country,
    string? Name,
    string OwnerFirstName,
    Guid OwnerId,
    string? OwnerLastName,
    decimal TransferBudget,
    decimal Value,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp);
