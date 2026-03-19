namespace Domain;

public record TeamDto(
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

public record InternalTeamDto(
    long InternalId,
    Guid ExternalId,
    string? Country,
    string? Name,
    string OwnerFirstName,
    string? OwnerLastName,
    decimal TransferBudget,
    decimal Value,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp) : TeamDto(
    ExternalId, 
    Country, 
    Name, 
    OwnerFirstName,
    OwnerLastName,
    TransferBudget,
    Value,
    CreatedAt, 
    UpdatedAt,
    ConcurrencyStamp);

public record TeamFilterDto(
    Guid? OwnerId = null,
    string? SearchTerm = null);
