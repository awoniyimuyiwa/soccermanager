using Api.Models.V1;
using Api.Services;
using Domain;
using Domain.BackgroundJobs;

namespace Api.Extensions;

public static class DtoExtensions
{
    public static AISettingModel ToModel(this AISettingDto dto) =>
        new(
            dto.Id,
            dto.CustomEndpoint,
            dto.Key,
            dto.Model,
            dto.Provider,
            dto.CreatedAt,
            dto.UpdatedAt);

    public static AuditLogModel ToModel(this AuditLogDto dto) =>
       new(dto.Id, 
           dto.BrowserInfo, 
           dto.Duration, 
           dto.Exception, 
           dto.HttpMethod,
           dto.IpAddress, 
           dto.RequestId,
           dto.StatusCode, 
           dto.Timestamp, 
           dto.Url, 
           dto.UserId);

    public static BackgroundJobModel ToModel(this BackgroundJobDto dto) =>
        new(
            dto.Id,
            dto.Attempts,
            dto.Error,
            dto.MaxRetries,
            dto.Payload,
            dto.Priority,
            dto.ScheduledFor,
            dto.SourceId,
            dto.Status,
            dto.TraceId,
            dto.Type,
            dto.CreatedAt,
            dto.UpdatedAt,
            dto.ConcurrencyStamp);

    public static AuditLogActionModel ToModel(this AuditLogActionDto dto) =>
        new(
            dto.ExecutionTime, 
            dto.MethodName, 
            dto.Parameters, 
            dto.ServiceName);

    public static BackgroundServiceStatModel ToModel(this BackgroundServiceStatDto dto) =>
        new(
            dto.Id,
            dto.Details,
            dto.LastRunAt,
            dto.Total,
            dto.TotalInLastRun,
            dto.Type);

    public static EntityChangeModel ToModel(this EntityChangeDto dto) =>
        new(
            dto.EntityName, 
            dto.NewValues, 
            dto.OldValues, 
            dto.Type);

    public static FullAuditLogModel ToModel(this FullAuditLogDto dto) =>
        new(
            dto.Id, 
            dto.BrowserInfo, 
            dto.Duration, 
            dto.Exception, 
            dto.HttpMethod,
            dto.IpAddress, 
            dto.RequestId, 
            dto.StatusCode, 
            dto.Timestamp, 
            dto.Url, 
            dto.UserId,
            [.. dto.AuditLogActions.Select(a => a.ToModel())],
            [.. dto.EntityChanges.Select(e => e.ToModel())]);

    public static PlayerModel ToModel(this PlayerDto dto) =>
        new(
            dto.Id,
            dto.Age,
            dto.Country,
            dto.DateOfBirth,
            dto.FirstName,
            dto.LastName,
            dto.TeamId,
            dto.TeamName,
            dto.Type,
            dto.Value,
            dto.CreatedAt,
            dto.UpdatedAt,
            dto.ConcurrencyStamp);

    public static PlayersModel ToModel(this IReadOnlyCollection<PlayerDto> dtos) =>
        new()
        {
            Players = dtos.Select(d => d.ToModel()).ToList().AsReadOnly()
        };

    public static TeamModel ToModel(this TeamDto dto) =>
        new(
            dto.Id,
            dto.Country,
            dto.Name,
            dto.OwnerFirstName,
            dto.OwnerLastName,
            dto.TransferBudget,
            dto.Value,
            dto.CreatedAt,
            dto.UpdatedAt,
            dto.ConcurrencyStamp);

    public static TransferModel ToModel(this TransferDto dto) =>
       new(
           dto.Id,
           dto.AskingPrice,
           dto.FromTeamId,
           dto.PlayerId,
           dto.ToTeamId,
           dto.CreatedAt,
           dto.UpdatedAt,
           dto.ConcurrencyStamp);

    public static FullTransferModel ToModel(this FullTransferDto dto) =>
        new(
            dto.Id,
            dto.AskingPrice,
            dto.FromTeamId,
            dto.FromTeamName,
            dto.PlayerFirstName,
            dto.PlayerId,
            dto.PlayerLastName,
            dto.ToTeamId,
            dto.ToTeamName,
            dto.CreatedAt,
            dto.UpdatedAt,
            dto.ConcurrencyStamp);

    public static UserModel ToModel(this UserDto dto) =>
       new(
           dto.Email,
           dto.FirstName,
           dto.Id,
           dto.IsEmailConfirmed,
           dto.LastName,
           dto.LockoutEnd,
           dto.UserName,
           dto.CreatedAt,
           dto.UpdatedAt,
           dto.ConcurrencyStamp);

    public static UserSessionModel ToModel(this UserSessionDto dto) =>
        new(
            Id: dto.SessionIdHash,
            AuthScheme: dto.AuthScheme,
            CorrelationId: dto.CorrelationId,
            DeviceInfo: dto.DeviceInfo,
            IpAddress: dto.IpAddress,
            LastSeen: dto.LastSeen,
            Location: dto.Location,
            LoginTime: dto.LoginTime);

    public static CursorListModel<TDestination> ToModel<TSource, TDestination>(
        this CursorList<TSource> source,
        Func<TSource, TDestination> selector)
        where TSource : class
        where TDestination : class
    {
        var mappedItems = source.Items
            .Select(selector)
            .ToList();

        return new CursorListModel<TDestination>(
            Items: mappedItems,
            Next: source.Next,
            PageSize: source.PageSize);
    }

    public static PaginatedListModel<TDestination> ToModel<TSource, TDestination>(
        this PaginatedList<TSource> source,
        Func<TSource, TDestination> selector)
        where TSource : class
        where TDestination : class
    {
        var mappedItems = source.Items
            .Select(selector)
            .ToList();

        return new PaginatedListModel<TDestination>(
            Items: mappedItems,
            TotalCount: source.TotalCount,
            PageNumber: source.PageNumber,
            TotalPages: source.TotalPages,
            HasPreviousPage: source.HasPreviousPage,
            HasNextPage: source.HasNextPage);
    }
}
