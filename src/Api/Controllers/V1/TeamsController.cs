using Api.Attributes;
using Api.Extensions;
using Api.Models.V1;
using Application.Contracts;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

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
    [ProducesResponseType(typeof(PaginatedListModel<TeamModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedListModel<TeamModel>>> Index(
        [FromQuery] [MaxLength(Domain.Constants.StringMaxLength)] string? search = null,
        [FromQuery] int pageNumber = Domain.Constants.MinPageNumber,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize)
    {
        // HttpContext.RequestAborted is used to avoid CS1573 warning 
        // and also not clutter docs with a cancellation token parameter.   
        var teams = await _teamRepository.Paginate(
            new TeamFilterDto(null, search),
            pageNumber,
            pageSize,
            HttpContext.RequestAborted);

        return Ok(teams.ToModel(t => t.ToModel()));
    }

    /// <summary>
    /// Stream teams using cursor-based pagination.
    /// </summary>
    /// <param name="search">Search by name</param>
    /// <param name="next">The opaque token for the next page. Pass <c>null</c> to start at the beginning.</param>
    /// <param name="pageSize">The number of records to return per batch.</param>
    /// <response code="200">Returns a cursor-paginated list of teams.</response>
    [HttpGet("stream")]
    [ProducesResponseType(typeof(CursorListModel<TeamModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorListModel<TeamModel>>> Stream(
        [FromQuery] [MaxLength(Domain.Constants.StringMaxLength)] string? search = null,
        [FromQuery] [MaxLength(Domain.Constants.StringMaxLength)] string? next = null,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize)
    {
        var cursor = next.ToCursor<TeamModel>(_dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Constants.InvalidErrorMessage);
            return ValidationProblem();
        }

        var teams = await _teamRepository.Stream(
            new TeamFilterDto(null, search),
            cursor,
            pageSize,
            HttpContext.RequestAborted);

        return Ok(teams.ToModel(t => t.ToModel()));
    }

    /// <summary>
    /// Create team for the currently logged in user
    /// </summary>
    /// <param name="input"></param>
    /// <response code="200">When there are no errors</response>
    /// <response code="400">When there are validation errors</response>
    /// <response code="401">When authentication fails</response>
    [HttpPost]
    [Idempotent]
    [ProducesResponseType(typeof(TeamModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [RequestTimeout(60000)]
    public async Task<ActionResult<TeamModel>> Create(CreateTeamModel input)
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
        }

        if (await _teamRepository.Any(t => t.ExternalId == input.Id))
        {
            ModelState.AddModelError(nameof(input.Id), Constants.AlreadyExistsErrorMessage);
        }

        if (input.Players.Count != 0)
        {
            await Validate(input.Players);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        var dto = await _teamService.Create(        
            user,
            input.ToDto(),
            [.. input.Players!.Select(p => p.ToDto())]);

        return Ok(dto.ToModel());
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
    [ProducesResponseType(typeof(PaginatedListModel<TeamModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaginatedListModel<TeamModel>>> GetUserTeams(
        [FromQuery] [MaxLength(Domain.Constants.StringMaxLength)] string? search = null,
        [FromQuery] int pageNumber = Domain.Constants.MinPageNumber,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize)
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
            HttpContext.RequestAborted);

        return Ok(teams.ToModel(t => t.ToModel()));
    }

    /// <summary>
    /// Get details of team with <paramref name="id"/>
    /// </summary>
    /// <param name="id">Team id</param>
    /// <response code="200">When there are no errors</response>
    /// <response code="404">When team is not found </response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TeamModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamModel>> View(Guid id)
    {
        var dto = await _teamRepository.Get(t => t.ExternalId == id);
        if (dto is null)
        {
            return NotFound();
        }

        return Ok(dto.ToModel());
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
    [Idempotent]
    [ProducesResponseType(typeof(PlayersModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayersModel>> CreatePlayers(
        Guid id, 
        CreatePlayersModel input)
    {
        await Validate(input.Players);

        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        var userId = User.GetUserId();

        var players = await _teamService.AddPlayers(    
            id,
            userId,
            new AddPlayersDto()
            {
                Players = [.. input.Players.Select(p => p.ToDto())],
                TeamConcurrencyStamp = input.TeamConcurrencyStamp
            });

        return Ok(players.ToModel());
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
    [ProducesResponseType(typeof(PaginatedListModel<PlayerModel>), StatusCodes.Status200OK)]
    [HttpGet("{id}/players")]
    public async Task<ActionResult<PaginatedListModel<PlayerModel>>> Players(
        [FromRoute] Guid id,
        [FromQuery] [MaxLength(Domain.Constants.StringMaxLength)] string? search = null,
        [FromQuery] int pageNumber = Domain.Constants.MinPageNumber,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize)
    {    
        var players = await _playerRepository.Paginate(
            new PlayerFilterDto(null, search, id),
            pageNumber,
            pageSize,
            HttpContext.RequestAborted);

        return Ok(players.ToModel(p => p.ToModel()));
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
    [ProducesResponseType(typeof(CursorListModel<PlayerModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorListModel<PlayerModel>>> StreamPlayers(
        [FromRoute] Guid id,
        [FromQuery] [MaxLength(Domain.Constants.StringMaxLength)] string? search = null,
        [FromQuery] [MaxLength(Domain.Constants.StringMaxLength)] string? next = null,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize)
    {
        var cursor = next.ToCursor<PlayerModel>(_dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Constants.InvalidErrorMessage);
            return ValidationProblem();
        }

        var players = await _playerRepository.Stream(
            new PlayerFilterDto(null, search, id),
            cursor,
            pageSize,
            HttpContext.RequestAborted);

        return Ok(players.ToModel(p => p.ToModel()));
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
    [ProducesResponseType(typeof(TeamModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TeamModel>> Update(
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

        var dto = await _teamService.Update(   
            id,
            userId,
            input.ToDto());

        return Ok(dto.ToModel());
    }

    private async Task Validate(
        IReadOnlyCollection<CreatePlayerModel> 
        players,
        CancellationToken cancellationToken = default)
    {
        var seenIds = new HashSet<Guid>();
        for (int i = 0; i < players.Count; i++)
        {
            var player = players.ElementAt(i);
            if (!seenIds.Add(player.Id))
            {
                var key = $"{nameof(CreateTeamModel.Players)}[{i}].{nameof(CreatePlayerModel.Id)}";
                ModelState.AddModelError(key, Constants.DuplicateInRequestErrorMessage);
            }
        }

        var ids = players.Select(p => p.Id)
            .Distinct()
            .ToList();
        var existingIds = await _playerRepository.GetExistingIds(
            ids, 
            cancellationToken);
        if (existingIds.Count == 0) { return; }

        var idPairs = existingIds.ToDictionary(id => id);

        for (int i = 0; i < players.Count; i++)
        {
            if (idPairs.ContainsKey(players.ElementAt(i).Id))
            {
                // Key format: "Players[0].Id"
                var key = $"{nameof(CreateTeamModel.Players)}[{i}].{nameof(CreatePlayerModel.Id)}";
                ModelState.AddModelError(key, Constants.AlreadyExistsErrorMessage);
            }
        }
    }
}
