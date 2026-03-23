namespace Api.Models.V1;

public record TransferModel(
    Guid Id,
    decimal AskingPrice,
    Guid FromTeamId,
    Guid PlayerId,
    Guid? ToTeamId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp);

public record FullTransferModel(
    Guid Id,
    decimal AskingPrice,
    Guid FromTeamId,
    string? FromTeamName,
    string? PlayerFirstName,
    Guid PlayerId,
    string? PlayerLastName,
    Guid? ToTeamId,
    string? ToTeamName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp)
    : TransferModel(
        Id, 
        AskingPrice, 
        FromTeamId, 
        PlayerId, 
        ToTeamId, 
        CreatedAt, 
        UpdatedAt, 
        ConcurrencyStamp);