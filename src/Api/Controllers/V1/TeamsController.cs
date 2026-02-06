using Api.Models.V1;
using Application.Contracts;
using Application.Services;
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
    IPlayerService playerService,
    ITeamRepository teamRepository,
    ITeamService teamService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    readonly IPlayerRepository _playerRepository = playerRepository;
    readonly IPlayerService _playerService = playerService;
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
    /// Create team for the currently logged in user
    /// </summary>
    /// <param name="input"></param>
    /// <response code="200">When there are no errors</response>
    /// <response code="400">When there are validation errors</response>
    /// <response code="401">When authentication fails</response>
    [HttpPost]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TeamDto>> Create(CreateTeamModel input)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        if (await _teamRepository.Any(
            t => t.OwnerId == user.Id && t.Name == input.Name))
        {
            ModelState.AddModelError(nameof(input.Name), Constants.AlreadyExistsErrorMessage);
            return ValidationProblem();
        }

        var team = await _teamService.Create(        
            user,
            input,
            [.. input.Players.Select(p => p as CreatePlayerDto)]);

        return Ok(team);
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
        var owner = await _userManager.GetUserAsync(User);
        if (owner is null)
        {
            return Unauthorized();
        }

        var teams = await _teamRepository.Paginate(
            owner.ExternalId,
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
        var team = await _teamRepository.Get(t => t.ExternalId == id);
        if (team is null)
        {
            return NotFound();
        }

        return Ok(team);
    }

    /// <summary>
    /// Create players for the team with the specified <paramref name="id"/> owned by the logged in user
    /// </summary>
    /// <param name="id">Team id</param>
    /// <param name="input"></param>
    /// <response code="200">When there are no errors</response>
    /// <response code="400">When there are validation errors</response>
    /// <response code="401">When authentication fails</response>
    /// <response code="404">When team is not found </response>
    [HttpPost("{id}/players")]
    [ProducesResponseType(typeof(PlayersModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayersModel>> CreatePlayers(
        Guid id, 
        CreatePlayersModel input)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var players = await _teamService.AddPlayers(
                id,
                long.Parse(userId),
                new AddPlayersDto()
                {
                    Players =  [..input.Players.Select(p => p as CreatePlayerDto)],
                    TeamConcurrencyStamp = input.TeamConcurrencyStamp
                });

            return Ok(new PlayersModel
            {
                Players = players
            });
        }
        catch (EntityNotFoundException e)
        {
            return NotFound(e.Message);
        }
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
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TeamDto>> Update(
        Guid id,
        UpdateTeamModel input)
    {
        var userIdString = _userManager.GetUserId(User);
        if (userIdString is null)
        {
            return Unauthorized();
        }

        var userId = long.Parse(userIdString);
        if (await _teamRepository.Any(
            t => t.OwnerId == userId
                 && t.ExternalId != id
                 && t.Name == input.Name))
        {
            ModelState.AddModelError(nameof(input.Name), Constants.AlreadyExistsErrorMessage);
            return ValidationProblem();
        }

        try
        {
            var team = await _teamService.Update(
                id,
                userId,
                input);

            return Ok(team);
        }
        catch (EntityNotFoundException e)
        {
            return NotFound(e.Message);
        }
    }
}
