using Api.Attributes;
using Api.Extensions;
using Api.Models.V1;
using Application.Contracts;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1;

[ApiController]
[Authorize]
[Audited]
[Route("v{version:apiVersion}/teams")]
public class TeamsController(
    IDataProtector dataProtector,
    IPlayerRepository playerRepository,
    ITeamRepository teamRepository,
    ITeamService teamService,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    readonly IDataProtector _dataProtector = dataProtector;
    readonly IPlayerRepository _playerRepository = playerRepository;
    readonly ITeamRepository _teamRepository = teamRepository;
    readonly ITeamService _teamService = teamService;
    readonly UserManager<ApplicationUser> _userManager = userManager;

    /// <summary>
    /// Get all teams
    /// </summary>
    /// <param name="search">Search by name</param>
    /// <param name="pageNumber">
    /// The 1-based page index. Values are clamped between 1 and the maximum allowed 
    /// offset limit (currently {50000 / pageSize + 1}).
    /// </param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<TeamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<TeamDto>>> Index(
        [FromQuery] string search = "",
        [FromQuery] int pageNumber = Domain.Constants.MinPageNumber,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        var teams = await _teamRepository.Paginate(
            new TeamFilterDto(null, search),
            pageNumber,
            pageSize,
            cancellationToken);

        return Ok(teams);
    }

    /// <summary>
    /// Stream teams using cursor-based pagination.
    /// </summary>
    /// <param name="search">Search by name</param>
    /// <param name="next">The opaque token for the next page. Pass <c>null</c> to start at the beginning.</param>
    /// <param name="pageSize">The number of records to return per batch.</param>
    /// <response code="200">Returns a cursor-paginated list of teams.</response>
    [HttpGet("stream")]
    [ProducesResponseType(typeof(CursorList<TeamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorList<TeamDto>>> Stream(
        [FromQuery] string? search = null,
        [FromQuery] string? next = null,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        var cursor = next.ToCursor<TeamDto>(_dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Constants.InvalidErrorMessage);
            return ValidationProblem();
        }

        var teams = await _teamRepository.Stream(
            new TeamFilterDto(null, search),
            cursor,
            pageSize,
            cancellationToken);

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
    /// <param name="pageNumber">
    /// The 1-based page index. Values are clamped between 1 and the maximum allowed 
    /// offset limit (currently {50000 / pageSize + 1}).
    /// </param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <response code="200">When there are no errors</response>
    /// <response code="401">When authentication fails</response>
    [HttpGet("my-teams")]
    [ProducesResponseType(typeof(PaginatedList<TeamDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaginatedList<TeamDto>>> GetUserTeams(
        [FromQuery] string search = "",
        [FromQuery] int pageNumber = Domain.Constants.MinPageNumber,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        var owner = await _userManager.GetUserAsync(User);
        if (owner is null)
        {
            return Unauthorized();
        }

        var teams = await _teamRepository.Paginate(
            new TeamFilterDto(owner.ExternalId, search),
            pageNumber,
            pageSize,
            cancellationToken);

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
        var userId = User.GetUserId();
        
        var players = await _teamService.AddPlayers(    
            id,
            userId,
            new AddPlayersDto()
            {
                Players = [.. input.Players.Select(p => p as CreatePlayerDto)],
                TeamConcurrencyStamp = input.TeamConcurrencyStamp
            });

        return Ok(new PlayersModel
        {
            Players = players
        });
    }

    /// <summary>
    /// Get players of team with <paramref name="id"/>
    /// </summary>
    /// <param name="id">Team id</param>
    /// <param name="search">Search by first name, last name etc.</param>
    /// <param name="pageNumber">
    /// The 1-based page index. Values are clamped between 1 and the maximum allowed 
    /// offset limit (currently {50000 / pageSize + 1}).
    /// </param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <response code="200">When there are no errors</response>
    [ProducesResponseType(typeof(PaginatedList<PlayerDto>), StatusCodes.Status200OK)]
    [HttpGet("{id}/players")]
    public async Task<ActionResult<PaginatedList<PlayerDto>>> Players(
        [FromRoute] Guid id,
        [FromQuery] string search = "",
        [FromQuery] int pageNumber = Domain.Constants.MinPageNumber,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        var players = await _playerRepository.Paginate(
            new PlayerFilterDto(null, search, id),
            pageNumber,
            pageSize,
            cancellationToken);

        return Ok(players);
    }

    /// <summary>
    /// Stream players of team with <paramref name="id"/> using cursor-based pagination.
    /// </summary>
    /// <param name="id">Team id.</param>
    /// <param name="search">Search by first name, last name, etc.</param>
    /// <param name="next">The opaque token for the next page. Set to <c>null</c> to start at the beginning.</param>
    /// <param name="pageSize">The number of records to return per batch.</param>
    /// <response code="200">Returns a cursor-paginated list of players.</response>
    [HttpGet("{id}/players/stream")]
    [ProducesResponseType(typeof(CursorList<PlayerDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorList<PlayerDto>>> StreamPlayers(
        [FromRoute] Guid id,
        [FromQuery] string? search = null,
        [FromQuery] string? next = null,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        var cursor = next.ToCursor<PlayerDto>(_dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Constants.InvalidErrorMessage);
            return ValidationProblem();
        }

        var players = await _playerRepository.Stream(
            new PlayerFilterDto(null, search, id),
            cursor,
            pageSize,
            cancellationToken);

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
        var userId = User.GetUserId();

        if (await _teamRepository.Any(
            t => t.OwnerId == userId
                 && t.ExternalId != id
                 && t.Name == input.Name))
        {
            ModelState.AddModelError(nameof(input.Name), Constants.AlreadyExistsErrorMessage);
            return ValidationProblem();
        }

        var team = await _teamService.Update(   
            id,
            userId,
            input);

        return Ok(team);
    }
}
