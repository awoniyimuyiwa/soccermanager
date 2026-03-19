using Api.BackgroundServices;
using Api.Extensions;
using Api.Models.V1;
using Domain;
using Domain.BackgroundJobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Api.Controllers.V1.Admin;

[ApiController]
[Authorize(Roles = Domain.Constants.AdminRoleName)]
[Route("v{version:apiVersion}/admin/background-jobs")]
public class BackgroundJobsController(
    IBackgroundJobRepository backgroundJobRepository,
    IBackgroundJobTrigger backgroundJobTrigger,
    IBackgroundServiceStatRepository backgroundServiceStatRepository,
    IDataProtector dataProtector) : ControllerBase
{
    readonly IBackgroundJobRepository _backgroundJobRepository = backgroundJobRepository;
    readonly IBackgroundJobTrigger _backgroundJobTrigger = backgroundJobTrigger;
    readonly IBackgroundServiceStatRepository _backgroundServiceStatRepository = backgroundServiceStatRepository;
    readonly Lazy<IDataProtector> _dataProtector = new(() => dataProtector.CreateProtector("BackgroundJob"));

    /// <summary>
    /// Retrieves a paginated list of background jobs for administrative grids.
    /// </summary>
    /// <param name="filter">Filtering criteria (Status, Type, etc.).</param>
    /// <param name="pageNumber">The 1-based page index.</param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<BackgroundJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<BackgroundJobDto>>> Index(
        [FromQuery] GetBackgroundJobFilterDto? filter,
        [FromQuery] int pageNumber = Domain.Constants.MinPageNumber,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize)
    {
        var backgroundJobs = await _backgroundJobRepository.Paginate(
            filter,
            pageNumber,
            pageSize);

        return Ok(backgroundJobs);
    }

    /// <summary>
    /// Streams background jobs using cursor-based pagination for high-performance infinite scrolling.
    /// </summary>
    /// <param name="filter">Filtering criteria for jobs.</param>
    /// <param name="cursor">The opaque cursor token from the previous response.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet("stream")]
    [ProducesResponseType(typeof(CursorListModel<BackgroundJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorListModel<BackgroundJobDto>>> Stream(
        [FromQuery] GetBackgroundJobFilterDto? filter,
        [FromQuery] string? cursor = null)
    {
        var pageCursor = cursor.ToPageCursor(_dataProtector.Value, null);

        if (!string.IsNullOrWhiteSpace(cursor) && pageCursor is null)
        {
            ModelState.AddModelError(nameof(cursor), Constants.InvalidCursorErrorMessage);
            return ValidationProblem();
        }

        var backgroundJobs = await _backgroundJobRepository.Stream(
            filter,
            pageCursor);

        // Encapsulate pagination state into an opaque, tamper-proof cursor token
        var model = backgroundJobs.ToModel(
            _dataProtector.Value,
            null);

        return Ok(model);
    }

    /// <summary>
    /// Get details of background job with <paramref name="id"/>
    /// </summary>
    /// <param name="id">Background job id</param>
    /// <response code="200">When there are no errors</response>
    /// <response code="404">When background job is not found </response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FullAuditLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FullAuditLogDto>> View(Guid id)
    {
        var backgroundJob = await _backgroundJobRepository.Get(bj => bj.ExternalId == id);

        if (backgroundJob is null)
        {
            return NotFound();
        }

        return Ok(backgroundJob);
    }

    /// <summary>
    /// Process background jobs manually
    /// </summary>
    /// <response code="202">When there are no errors</response>
    [HttpPost("Process")]
    public IActionResult Process()
    {
        _backgroundJobTrigger.Trigger();

        return Accepted("Background job processing triggered manually.");
    }

    /// <summary>
    /// Re-activates background jobs that have reached their maximum retry limit or encountered a terminal failure.
    /// </summary>
    /// <param name="filter">Filtering criteria for jobs.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">When there are no errors</response>
    [HttpPost("requeue")]
    public async Task<IActionResult> Requeue(
        [FromQuery] RequeueBackgroundJobFilterDto? filter,
        CancellationToken cancellationToken)
    {
        var count = await _backgroundJobRepository.RequeueFailed(
            filter,
            cancellationToken);

        if (count > 0)
        {
            _backgroundJobTrigger.Trigger();
        }

        return Ok($"Requeued jobs: {count}");
    }
  
    /// <summary>
    /// Get background job processing status
    /// </summary>
    /// <response code="200">When there are no errors</response>
    [HttpGet("processing-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessingStatus()
    {
        var dto = await _backgroundServiceStatRepository.Get
            (bss => bss.Type == BackgroundServiceStatType.BackgroundJob);

        if (dto is null)
        {
            return NotFound("Background job processing has never run.");
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
