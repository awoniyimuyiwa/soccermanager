using Api.Models.V1;
using Application.Contracts;
using Domain;
using Domain.BackgroundJobs;

namespace Api.Extensions;

public static class ModelExtensions
{
    public static AuditLogFilterDto ToDto(this AuditLogFilterModel model) =>
       new(
           From: model.From,
           HttpMethod: model.HttpMethod,
           IpAddress: model.IpAddress,
           IsSuccessful: model.IsSuccessful,
           RequestId: model.RequestId,
           StatusCode: model.StatusCode,
           To: model.To,
           Url: model.Url,
           UserId: model.UserId);

    public static CreateUpdateAISettingDto ToDto(this CreateUpdateAISettingModel model) =>
        new(
            model.CustomEndpoint,
            model.Key,
            model.Model,
            model.Provider);

    public static CreatePlayerDto ToDto(this CreatePlayerModel model) =>
        new(
            model.Id,
            model.Country,
            model.DateOfBirth,
            model.FirstName,
            model.LastName,
            model.Type,
            model.Value);

    public static CreateTeamDto ToDto(this CreateTeamModel model) =>
        new(
            model.Id,
            model.Country,
            model.Name,
            model.TransferBudget);

    public static GetBackgroundJobFilterDto ToDto(this GetBackgroundJobFilterModel model) =>
        new(
            CreatedFrom: model.CreatedFrom,
            CreatedTo: model.CreatedTo,
            Priorities: model.Priorities,
            ScheduledFrom: model.ScheduledFrom,
            ScheduledTo: model.ScheduledTo,
            Statuses: model.Statuses,
            Types: model.Types,
            UpdatedFrom: model.UpdatedFrom,
            UpdatedTo: model.UpdatedTo);

    public static PayForTransferDto ToDto(this PayForTransferModel model) =>
        new(
            model.ToTeamId,
            model.ConcurrencyStamp);

    public static PlaceOnTransferListDto ToDto(this PlaceOnTransferListModel model) =>
        new(
            model.AskingPrice,
            model.PlayerConcurrencyStamp);

    public static RequeueBackgroundJobFilterDto ToDto(this RequeueBackgroundJobFilterModel model) =>
        new(
            CreatedFrom: model.CreatedFrom,
            CreatedTo: model.CreatedTo,
            Ids: model.Ids,
            Priorities: model.Priorities,
            ScheduledFrom: model.ScheduledFrom,
            ScheduledTo: model.ScheduledTo,
            SourceIds: model.SourceIds,
            TraceIds: model.TraceIds,
            Types: model.Types,
            UpdatedFrom: model.UpdatedFrom,
            UpdatedTo: model.UpdatedTo);

    public static UpdatePlayerDto ToDto(this UpdatePlayerModel model) =>
        new(
            model.Country,
            model.DateOfBirth,
            model.FirstName,
            model.LastName,
            model.Type,
            model.ConcurrencyStamp);

    public static UpdateTeamDto ToDto(this UpdateTeamModel model) =>
       new(
           model.Country,
           model.Name,
           model.ConcurrencyStamp);

    public static UserFilterDto ToDto(this UserFilterModel model) =>
        new(
            SearchTerm: model.Search,
            CreatedFrom: model.CreatedFrom,
            CreatedTo: model.CreatedTo,
            IsEmailConfirmed: model.IsEmailConfirmed,
            UpdatedFrom: model.UpdatedFrom,
            UpdatedTo: model.UpdatedTo);
}
