using Api.Attributes;
using Api.Extensions;
using Api.Models.V1;
using Application.Services;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1;

[ApiController]
[Authorize]
[Audited]
[Route("v{version:apiVersion}/players")]
public class PlayersController(
    IPlayerRepository playerRepository,
    IPlayerService playerService) : ControllerBase
{
    readonly IPlayerRepository _playerRepository = playerRepository;
    readonly IPlayerService _playerService = playerService;

    /// <summary>
    /// Get details of player with <paramref name="id"/>
    /// </summary>
    /// <param name="id">Player id</param>
    /// <response code="200">When there are no errors</response>
    /// <response code="404">When player is not found </response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PlayerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerDto>> View(Guid id)
    {
        var player = await _playerRepository.Get(p => p.ExternalId == id);

        if (player is null)
        {
            return NotFound();
        }

        return Ok(player);
    }

    /// <summary>
    /// Get scout report of player with <paramref name="id"/>
    /// </summary>
    /// <param name="id">Player id</param>
    /// <response code="200">When there are no errors</response>
    /// <response code="401">When the user is not authenticated</response>
    /// <response code="404">When the player or AI settings are not found</response>
    [HttpGet("{id}/scout-report")]
    [ProducesResponseType(typeof(ScoutReportModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScoutReportModel>> ScoutReport(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();

        var report = await _playerService.GetScoutReport(
            id, 
            userId,
            cancellationToken);

        return Ok(new ScoutReportModel
        {
            Report = report
        });
    }

    /// <summary>
    /// Place player with <paramref name="id"/> in a team owned by the logged in user on the transfer list if the player isn't already on transfer list
    /// </summary>
    /// <param name="id">Player id</param>
    /// <param name="input"></param>
    /// <response code="200">When there are no errors</response>
    /// <response code="400">When there are validation errors</response>
    /// <response code="401">When authentication fails</response>
    /// <response code="404">When player is not found </response>
    /// <response code="409">When concurrency error occurs</response>
    /// <response code="422">When request can't be processed</response>
    [HttpPost("{id}/transfer-list")]
    [ProducesResponseType(typeof(TransferDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PlayerDto>> PlaceOnTransferList(
        Guid id,
        PlaceOnTransferListModel input)
    {
        var userId = User.GetUserId();

        var transfer = await _playerService.PlaceOnTransferList(   
            id,
            userId,
            input);

        return Ok(transfer);
    }

    /// <summary>
    /// Update details of player with <paramref name="id"/> in a team owned by the logged in user
    /// </summary>
    /// <param name="id">Player id</param>
    /// <param name="input"></param>
    /// <response code="200">When there are no errors</response>
    /// <response code="400">When there are validation errors</response>
    /// <response code="401">When authentication fails</response>
    /// <response code="404">When player is not found </response>
    /// <response code="409">When concurrency error occurs</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(PlayerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PlayerDto>> Update(
        Guid id,
        UpdatePlayerModel input)
    {
        var userId = User.GetUserId();

        var player = await _playerService.Update(   
            id,
            userId,
            input);

        return Ok(player);
    }
}
