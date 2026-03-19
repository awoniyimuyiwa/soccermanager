using Api.Attributes;
using Api.Extensions;
using Api.Models.V1;
using Application.Contracts;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using static System.Net.Mime.MediaTypeNames;

namespace Api.Controllers.V1;

[ApiController]
[Authorize]
[Audited]
[Route("v{version:apiVersion}/transfers")]
public class TransfersController(
    IDataProtector dataProtector,
    ITransferRepository transferRepository,
    ITransferService transferService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    readonly IDataProtector _dataProtector = dataProtector;
    readonly ITransferRepository _transferRepository = transferRepository;
    readonly ITransferService _transferService = transferService;
    readonly UserManager<ApplicationUser> _userManager = userManager;

    /// <summary>
    /// Get all transfers
    /// </summary>
    /// <param name="isPending">Set to true for pending transfers, false for completed transfers</param>
    /// <param name="search">Search by team name, player first name, player last name</param>
    /// <param name="pageNumber">
    /// The 1-based page index. Values are clamped between 1 and the maximum allowed 
    /// offset limit (currently {50000 / pageSize + 1}).
    /// </param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<FullTransferDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<FullTransferDto>>> Index(
        [FromQuery] bool? isPending = null,
        [FromQuery] string search = "",
        [FromQuery] int pageNumber = Domain.Constants.MinPageNumber,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        var transfers = await _transferRepository.Paginate(
            new TransferFilterDto(isPending, null, search),
            pageNumber,
            pageSize,
            cancellationToken);

        return Ok(transfers);
    }

    /// <summary>
    /// Stream transfers using cursor-based pagination.
    /// </summary>
    /// <param name="isPending">Set to true for pending transfers, false for completed transfers.</param>
    /// <param name="search">Search by team name, player first name, or player last name.</param>
    /// <param name="next">The opaque token for the next page. Set to <c>null</c> to start at the beginning.</param>
    /// <param name="pageSize">The number of records to return per batch.</param>
    /// <response code="200">Returns a cursor-paginated list of transfers.</response>
    [HttpGet("stream")]
    [ProducesResponseType(typeof(CursorList<FullTransferDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorList<FullTransferDto>>> Stream(
        [FromQuery] bool? isPending = null,
        [FromQuery] string? search = null,
        [FromQuery] string? next = null,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        var cursor = next.ToCursor<FullTransferDto>(_dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Constants.InvalidErrorMessage);
            return ValidationProblem();
        }

        var transfers = await _transferRepository.Stream(
            new TransferFilterDto(isPending, null, search),
            cursor,
            pageSize,
            cancellationToken);

        return Ok(transfers);
    }

    /// <summary>
    /// Gets transfers for teams owned by logged in user
    /// </summary>
    /// <param name="isPending">Set to true for pending transfers, false for completed transfers</param>
    /// <param name="search">Search by team name, player first name, player last name</param>
    /// <param name="pageNumber">
    /// The 1-based page index. Values are clamped between 1 and the maximum allowed 
    /// offset limit (currently {50000 / pageSize + 1}).
    /// </param>
    /// <param name="pageSize"></param>
    /// <response code="200">When there are no errors</response>
    /// <response code="401">When authentication fails</response>
    [HttpGet("my-transfers")]
    [ProducesResponseType(typeof(PaginatedList<FullTransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaginatedList<FullTransferDto>>> GetUserTransfers(
        [FromQuery] bool? isPending = null,
        [FromQuery] string search = "",
        [FromQuery] int pageNumber = Domain.Constants.MinPageNumber,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        var user =  await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var transfers = await _transferRepository.Paginate(
            new TransferFilterDto(isPending, user.ExternalId, search),
            pageNumber,
            pageSize,
            cancellationToken);

        return Ok(transfers);
    }

    /// <summary>
    /// Get details of transfer with <paramref name="id"/>
    /// </summary>
    /// <param name="id">Transfer id</param>
    /// <response code="200">When there are no errors</response>
    /// <response code="404">When team is not found </response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FullTransferDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FullTransferDto>> View(Guid id)
    {
        var transfer = await _transferRepository.FindAsFullDto(t => t.ExternalId == id);
        if (transfer is null)
        {
            return NotFound();
        }

        return Ok(transfer);
    }

    /// <summary>
    /// Pay for transfer: <paramref name="id"/>, move player to destination team owned by logged in user specified in <paramref name="input"/> 
    /// update player value, source team and destination team tansfer budget and values.
    /// </summary>
    /// <param name="id">Transfer id</param>
    /// <param name="input"></param>
    /// <response code="200">When there are no errors</response>
    /// <response code="400">When there are validation errors</response>
    /// <response code="401">When authentication fails</response>
    /// <response code="404">When transfer is not found </response>
    /// <response code="409">When concurrency error occurs</response>
    /// <response code="422">When request can't be processed</response>
    [HttpPut("{id}/pay")]
    [ProducesResponseType(typeof(TransferDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransferDto>> Pay(
        Guid id,
        PayForTransferModel input)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized();
        }

        var transfer = await _transferService.Pay(    
            id,    
            input);

        return Ok(transfer);
    }
}
