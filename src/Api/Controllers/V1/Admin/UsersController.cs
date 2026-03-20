using Api.Extensions;
using Api.Models.V1;
using Api.Services;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Admin;

[ApiController]
[Authorize(Roles = Domain.Constants.AdminRoleName)]
[Route("v{version:apiVersion}/admin/users")]
public class UsersController(
    IDataProtector dataProtector,
    IUserRepository userRepository,
    IUserSessionManager sessionManager) : ControllerBase
{
    readonly IDataProtector _dataProtector = dataProtector;
    readonly IUserRepository _userRepository = userRepository;
    readonly IUserSessionManager _sessionManager = sessionManager;

    /// <summary>
    /// Get a paged list of users.
    /// </summary>
    /// <param name="filter">The filtering criteria for the audit logs.</param>
    /// <param name="pageNumber">
    /// The 1-based page index. Values are clamped between 1 and the maximum allowed 
    /// offset limit (currently {50000 / pageSize + 1}).
    /// </param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<UserDto>>> Index(
        [FromQuery] UserFilterDto? filter,
        [FromQuery] int pageNumber = Domain.Constants.MinPageNumber,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize)
    {
        var users = await _userRepository.Paginate(
            filter,
            pageNumber,
            pageSize);

        return Ok(users);          
    }

    /// <summary>
    /// Streams users using cursor-based pagination for high-performance infinite scrolling.
    /// </summary>
    /// <param name="filter">Filtering criteria for audit logs.</param>
    /// <param name="next">The opaque cursor token from the previous response. Pass null for the first page</param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet("stream")]
    [ProducesResponseType(typeof(CursorList<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorList<UserDto>>> Stream(
        [FromQuery] UserFilterDto? filter,
        [FromQuery] string? next,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize,
        CancellationToken cancellationToken = default)
    {
        var cursor = next.ToCursor<UserDto>(_dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Constants.InvalidErrorMessage);
            return ValidationProblem();
        }

        var users = await _userRepository.Stream(
            filter,
            cursor,
            pageSize,
            cancellationToken);

        return Ok(users);
    }

    /// <summary>
    /// Retrieves all active sessions for a specific user.
    /// </summary>
    /// <param name="userId">The unique id of the user.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet("{userId:long}/sessions")]
    [ProducesResponseType(typeof(SessionsModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> ViewSessions(long userId)
    {
        var sessions = await _sessionManager.GetAll(userId);

        return Ok(new SessionsModel { Sessions = sessions });
    }

    /// <summary>
    /// Revokes a specific session for a user.
    /// </summary>
    /// <param name="userId">The unique id of the user.</param>
    /// <param name="sessionIdHash">The unique hashed id of the session.</param>
    /// <response code="200">When there are no errors</response>
    [HttpDelete("{userId:long}/sessions/{sessionIdHash}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeSession(
        long userId,
        string sessionIdHash)
    {
        await _sessionManager.Remove(
            userId,
            sessionIdHash);     
        return Ok(new 
        { 
            message = $"Session {sessionIdHash} for user {userId} has been revoked." 
        });
    }

    /// <summary>
    /// Forces a global logout for a specific user.
    /// </summary>
    /// <param name="userId">The unique id of the user.</param>
    /// <response code="200">When there are no errors</response>
    [HttpPost("{userId:long}/revoke-sessions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeSessions(long userId)
    {
        await _sessionManager.RemoveAll(userId);

        return Ok(new 
        { 
            message = $"User {userId} has been globally logged out." 
        });
    }
}