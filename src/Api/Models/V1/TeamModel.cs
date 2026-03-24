namespace Api.Models.V1;

public record TeamModel(
    Guid Id,
    string? Country,
    string? Name,
    string OwnerFirstName,
    string? OwnerLastName,
    decimal TransferBudget,
    decimal Value,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp);




