using Api.Models.V1;
using Application.Contracts;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1;

[ApiController]
[Authorize]
[Route("v{version:apiVersion}/teams")]
public class TeamsController(
    IPlayerRepository playerRepository,
    ITeamRepository teamRepository,
    ITeamService teamService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    readonly IPlayerRepository _playerRepository = playerRepository;
    readonly ITeamRepository _teamRepository = teamRepository;
    readonly ITeamService _teamService = teamService;
    readonly UserManager<ApplicationUser> _userManager = userManager;

    /// <summary>
    /// Get all teams
    /// </summary>
    /// <param name="search">Search by name</param>
    /// <param name="pageNumber"></param>
    /// <param name="pageSize"></param>
    /// <response code="200">When there are no errors</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<TeamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<TeamDto>>> Index(
        string search = "",
        int pageNumber = Domain.Constants.MinPageNumber,
        int pageSize = Domain.Constants.MaxPageSize)
    {
        var teams = await _teamRepository.Paginate(
            null,
            search,
            pageNumber,
            pageSize);

        return Ok(teams);
    }

    /// <summary>
    /// Get teams owned by logged in user
    /// </summary>
    /// <param name="search">Search by name</param>
    /// <param name="pageNumber"></param>
    /// <param name="pageSize"></param>
    /// <response code="200">When there are no errors</response>
    /// <response code="401">When authentication fails</response>
    [HttpGet("my-teams")]
    [ProducesResponseType(typeof(PaginatedList<TeamDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaginatedList<TeamDto>>> GetUserTeams(
        string search = "",
        int pageNumber = Domain.Constants.MinPageNumber,
        int pageSize = Domain.Constants.MaxPageSize)
    {
        var ownerId = _userManager.GetUserId(User);
        if (ownerId is null)
        {
            return Unauthorized();
        }

        var teams = await _teamRepository.Paginate(
            Guid.Parse(ownerId),
            search,
            pageNumber,
            pageSize);

        return Ok(teams);
    }

    /// <summary>
    /// Get details of team with <paramref name="id"/>
    /// </summary>
    /// <param name="id">Team id</param>
    /// <response code="200">When there are no errors</response>
    /// <response code="404">When team is not found </response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamDto>> View(Guid id)
    {
        var team = await _teamRepository.Get(t => t.Id == id);
        if (team is null)
        {
            return NotFound();
        }

        return Ok(team);
    }

    /// <summary>
    /// Get players of team with <paramref name="id"/>
    /// </summary>
    /// <param name="id">Team id</param>
    /// <param name="search">Search by first name, last name etc.</param>
    /// <param name="pageNumber"></param>
    /// <param name="pageSize"></param>
    /// <response code="200">When there are no errors</response>
    [ProducesResponseType(typeof(PaginatedList<PlayerDto>), StatusCodes.Status200OK)]
    [HttpGet("{id}/players")]
    public async Task<ActionResult<PaginatedList<PlayerDto>>> Players(
        Guid id,
        string search = "",
        int pageNumber = Domain.Constants.MinPageNumber,
        int pageSize = Domain.Constants.MaxPageSize)
    {
        var players = await _playerRepository.Paginate(
            id,
            null,
            search,
            pageNumber,
            pageSize);

        return Ok(players);
    }

    /// <summary>
    /// Update details of team with <paramref name="id"/> that is owned by the logged in user
    /// </summary>
    /// <param name="id">Team id</param>
    /// <param name="input"></param>
    /// <response code="200">When there are no errors</response>
    /// <response code="400">When there are validation errors</response>
    /// <response code="401">When authentication fails</response>
    /// <response code="404">When team is not found </response>
    /// <response code="409">When concurrency error occurs</response>
    [HttpPut("my-teams/{id}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TeamDto>> Update(
        Guid id,
        UpdateTeamModel input)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var team = await _teamService.Update(
                id,
                Guid.Parse(userId!),
                input);

            return Ok(team);
        }
        catch (EntityNotFoundException e)
        {
            return NotFound(e.Message);
        }
    }
}
