using Api.BackgroundServices;
using Api.Extensions;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Api.Controllers.V1.Admin;

[ApiController]
[Authorize(Roles = Domain.Constants.AdminRoleName)]
[Route("v{version:apiVersion}/admin/audit-logs")]
public class AuditLogsController(
    IAuditLogRepository auditLogRepository,
    IBackgroundServiceStatRepository backgroundServiceStatRepository,
    IDataProtector dataProtector) : ControllerBase
{
    readonly IAuditLogRepository _auditLogRepository = auditLogRepository;
    readonly IBackgroundServiceStatRepository _backgroundServiceStatRepository = backgroundServiceStatRepository;
    readonly IDataProtector _dataProtector = dataProtector;

    /// <summary>
    /// Get a paged list of audit logs.
    /// </summary>
    /// <param name="filter">The filtering criteria for the audit logs.</param>
    /// <param name="pageNumber">
    /// The 1-based page index. Values are clamped between 1 and the maximum allowed 
    /// offset limit (currently {50000 / pageSize + 1}).
    /// </param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<AuditLogDto>>> Index(
        [FromQuery] AuditLogFilterDto filter,
        [FromQuery] int pageNumber = Domain.Constants.MinPageNumber,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        var auditLogs = await _auditLogRepository.Paginate(
            filter,
            pageNumber,
            pageSize,
            cancellationToken);

        return Ok(auditLogs);
    }

    /// <summary>
    /// Streams audit logs using cursor-based pagination for high-performance infinite scrolling.
    /// </summary>
    /// <param name="filter">Filtering criteria for audit logs.</param>
    /// <param name="next">The opaque cursor token from the previous response. Pass null for the first page</param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet("stream")]
    [ProducesResponseType(typeof(CursorList<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorList<AuditLogDto>>> Stream(
        [FromQuery] AuditLogFilterDto? filter,
        [FromQuery] string? next,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        var cursor = next.ToCursor<AuditLogDto>(_dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Constants.InvalidErrorMessage);
            return ValidationProblem();
        }

        var auditLogs = await _auditLogRepository.Stream(
            filter,
            cursor,
            pageSize,
            cancellationToken);

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
            ? JsonSerializer.Deserialize<object>(
                dto.Details, 
                JsonSerializerOptions.Web) : null,
            dto.LastRunAt,
            dto.TotalInLastRun,
            dto.Total
        });
    }
}
