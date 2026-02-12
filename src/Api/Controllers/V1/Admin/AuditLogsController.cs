using Api.BackgroundServices;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Api.Controllers.V1.Admin;

[ApiController]
[Authorize(Roles = Domain.Constants.AdminRoleName)]
[Route("v{version:apiVersion}/admin/audit-logs")]
public class AuditLogsController(
    IAuditLogRepository auditLogRepository,
    IBackgroundServiceStatRepository backgroundServiceStatRepository) : ControllerBase
{
    readonly IAuditLogRepository _auditLogRepository = auditLogRepository;
    readonly IBackgroundServiceStatRepository _backgroundServiceStatRepository = backgroundServiceStatRepository;
   
    /// <summary>
    /// Get audit logs
    /// </summary>
    /// <param name="from">Minimum timestamp in ISO 8601 format (value specified is inclusive)</param>
    /// <param name="httpMethod"></param>
    /// <param name="ipAddress"></param>
    /// <param name="to">Maximum timestamp ISO 8601 format (value specified is inclusive)</param>
    /// <param name="url"></param>
    /// <param name="isSuccessful"></param>
    /// <param name="requestId"></param>
    /// <param name="userId"></param>
    /// <param name="statusCode"></param>
    /// <param name="pageNumber"></param>
    /// <param name="pageSize"></param>
    /// <response code="200">When there are no errors</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<AuditLogDto>>> Index(
        DateTimeOffset? from = null,
        string? httpMethod = null,
        string? ipAddress = null,
        bool? isSuccessful = null,
        string? requestId = null,
        int? statusCode = null,
        DateTimeOffset? to = null,
        string? url = null,
        Guid? userId = null,
        int pageNumber = Domain.Constants.MinPageNumber,
        int pageSize = Domain.Constants.MaxPageSize)
    {
        var auditLogs = await _auditLogRepository.Paginate(
            from,
            httpMethod,
            ipAddress,
            isSuccessful,
            requestId,
            statusCode,
            to,
            url,
            userId,
            pageNumber,
            pageSize);

        return Ok(auditLogs);
    }

    /// <summary>
    /// Get details of audit log with <paramref name="id"/>
    /// </summary>
    /// <param name="id">Audit log id</param>
    /// <response code="200">When there are no errors</response>
    /// <response code="404">When audit log is not found </response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FullAuditLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FullAuditLogDto>> View(Guid id)
    {
        var auditLog = await _auditLogRepository.Get(al => al.ExternalId == id);

        if (auditLog is null)
        {
            return NotFound();
        }

        return Ok(auditLog);
    }

    /// <summary>
    /// Clean up audit logs manually
    /// </summary>
    /// <response code="202">When there are no errors</response>
    [HttpPost("cleanup")]
    public IActionResult Cleanup([FromServices] IAuditLogCleanupTrigger trigger)
    {
        trigger.Trigger();

        return Accepted("Audit log cleanup triggered manually.");
    }

    /// <summary>
    /// Get audit log clean up status
    /// </summary>
    /// <response code="200">When there are no errors</response>
    [HttpGet("cleanup-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CleanupStatus()
    {
        var dto = await _backgroundServiceStatRepository.Get
            (bss => bss.Type == BackgroundServiceStatType.AuditLogCleanUp);

        if (dto is null)
        {
            return NotFound("Audit log cleanup has never run.");
        }

        return Ok(new
        {
            Details = !string.IsNullOrWhiteSpace(dto.Details)
            ? JsonSerializer.Deserialize<object>(dto.Details) : null,
            dto.LastRunAt,
            dto.TotalInLastRun,
            dto.Total
        });
    }
}
