namespace Domain;

public record TransferDto(
    Guid Id,
    decimal AskingPrice,
    Guid FromTeamId,
    Guid PlayerId,
    Guid? ToTeamId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? ConcurrencyStamp) { }

public record FullTransferDto(
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
    string? ConcurrencyStamp) : TransferDto(
        Id, 
        AskingPrice, 
        FromTeamId, 
        PlayerId,
        ToTeamId,
        CreatedAt,
        UpdatedAt,
        ConcurrencyStamp) {}
    
