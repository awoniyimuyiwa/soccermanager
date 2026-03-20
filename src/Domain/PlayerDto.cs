namespace Domain;

public record PlayerDto(
    Guid Id,
    int Age,
    string? Country,
    DateOnly DateOfBirth,
    string? FirstName,
    string? LastName,
    Guid TeamId,
    string? TeamName,
    PlayerType Type,
    decimal Value,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp) { }

public record InternalPlayerDto(
    long InternalId,
    Guid ExternalId,
    int Age,
    string? Country,
    DateOnly DateOfBirth,
    string? FirstName,
    string? LastName,
    Guid TeamId,
    string? TeamName,
    PlayerType Type,
    decimal Value,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp) : PlayerDto(
    ExternalId, 
    Age, 
    Country,
    DateOfBirth,
    FirstName, 
    LastName,
    TeamId, 
    TeamName, 
    Type, 
    Value, 
    CreatedAt, 
    UpdatedAt, 
    ConcurrencyStamp), IHasInternalCursor;

public record PlayerFilterDto(
    Guid? OwnerId = null,
    string? SearchTerm = null,
    Guid? TeamId = null);