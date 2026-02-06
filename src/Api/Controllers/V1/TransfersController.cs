using Api.Models.V1;
using Application.Contracts;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1;

[ApiController]
[Authorize]
[Route("v{version:apiVersion}/transfers")]
public class TransfersController(
    ITransferRepository transferRepository,
    ITransferService transferService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    readonly ITransferRepository _transferRepository = transferRepository;
    readonly ITransferService _transferService = transferService;
    readonly UserManager<ApplicationUser> _userManager = userManager;

    /// <summary>
    /// Get all transfers
    /// </summary>
    /// <param name="isPending">Set to true for pending transfers, false for completed transfers</param>
    /// <param name="search">Search by team name, player first name, player last name</param>
    /// <param name="pageNumber"></param>
    /// <param name="pageSize"></param>
    /// <response code="200">When there are no errors</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<FullTransferDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<FullTransferDto>>> Index(
        bool? isPending = null,
        string search = "",
        int pageNumber = Domain.Constants.MinPageNumber,
        int pageSize = Domain.Constants.MaxPageSize)
    {
        var transfers = await _transferRepository.Paginate(
            isPending,
            null,
            search,
            pageNumber,
            pageSize);

        return Ok(transfers);
    }

    /// <summary>
    /// Gets transfers for teams owned by loged in user
    /// </summary>
    /// <param name="isPending">Set to true for pending transfers, false for completed transfers</param>
    /// <param name="search">Search by team name, player first name, player last name</param>
    /// <param name="pageNumber"></param>
    /// <param name="pageSize"></param>
    /// <response code="200">When there are no errors</response>
    /// <response code="401">When authentication fails</response>
    [HttpGet("my-transfers")]
    [ProducesResponseType(typeof(PaginatedList<FullTransferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaginatedList<FullTransferDto>>> GetUserTransfers(
        bool? isPending = null,
        string search = "",
        int pageNumber = Domain.Constants.MinPageNumber,
        int pageSize = Domain.Constants.MaxPageSize)
    {
        var user =  await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var transfers = await _transferRepository.Paginate(
            isPending,
            user.ExternalId,
            search,
            pageNumber,
            pageSize);

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
    /// Pay for transfer: <paramref name="id"/>, move player to destination team owned by logged in user, 
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

        try
        {
            var transfer = await _transferService.Pay(
                id,
                long.Parse(userId),
                input.ConcurrencyStamp);
            return Ok(transfer);
        }
        catch (EntityNotFoundException e)
        {
            return NotFound(e.Message);
        }
        catch (DomainException e)
        {
            // .NET intercepts 403s so use 422
            return UnprocessableEntity(e.Message);
        }
    }
}
