using Api.Extensions;
using Api.Models.V1;
using Api.Services;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

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
    [ProducesResponseType(typeof(PaginatedListModel<UserModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedListModel<UserModel>>> Index(
        [FromQuery] UserFilterModel? filter,
        [FromQuery] int pageNumber = Domain.Constants.MinPageNumber,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize)
    {
        var users = await _userRepository.Paginate(
            filter?.ToDto(),
            pageNumber,
            pageSize,
            HttpContext.RequestAborted);

        return Ok(users.ToModel(u => u.ToModel()));          
    }

    /// <summary>
    /// Streams users using cursor-based pagination for high-performance infinite scrolling.
    /// </summary>
    /// <param name="filter">Filtering criteria for audit logs.</param>
    /// <param name="next">The opaque cursor token from the previous response. Pass null for the first page</param>
    /// <param name="pageSize">The number of records per page.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet("stream")]
    [ProducesResponseType(typeof(CursorListModel<UserModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorListModel<UserModel>>> Stream(
        [FromQuery] UserFilterModel? filter,
        [FromQuery] [MaxLength(Domain.Constants.StringMaxLength)] string? next,
        [FromQuery] int pageSize = Domain.Constants.MaxPageSize)
    {
        var cursor = next.ToCursor<UserModel>(_dataProtector);

        if (!string.IsNullOrWhiteSpace(next) && cursor is null)
        {
            ModelState.AddModelError(nameof(next), Constants.InvalidErrorMessage);
            return ValidationProblem();
        }

        var users = await _userRepository.Stream(
            filter?.ToDto(),
            cursor,
            pageSize,
            HttpContext.RequestAborted);

        return Ok(users.ToModel(u => u.ToModel()));
    }

    /// <summary>
    /// Retrieves all active sessions for a specific user.
    /// </summary>
    /// <param name="userId">The unique id of the user.</param>
    /// <response code="200">When there are no errors</response>
    [HttpGet("{userId:long}/sessions")]
    [ProducesResponseType(typeof(SessionsModel), StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionsModel>> ViewSessions(long userId)
    {
        var sessions = await _sessionManager.GetAll(userId);

        return Ok(new SessionsModel 
        { 
            Sessions = [.. sessions.Select(s => s.ToModel())],
        });
    }

    /// <summary>
    /// Revokes a specific session for a user.
    /// </summary>
    /// <param name="userId">The unique id of the user.</param>
    /// <param name="sessionId">The id of the session.</param>
    /// <response code="200">When there are no errors</response>
    [HttpDelete("{userId:long}/sessions/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeSession(
        long userId,
        string sessionId)
    {
        await _sessionManager.Remove(
            userId,
            sessionId);

        return Ok($"Session {sessionId} for user {userId} has been revoked.");
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

        return Ok($"User {userId} has been globally logged out.");
    }
}