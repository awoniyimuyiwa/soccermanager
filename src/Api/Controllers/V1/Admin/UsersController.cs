using Api.Models.V1;
using Api.Services;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.V1.Admin;

[ApiController]
[Authorize(Roles = Domain.Constants.AdminRoleName)]
[Route("v{version:apiVersion}/admin/users")]
public class UsersController(
    IUserRepository userRepository,
    IUserSessionManager sessionManager) : ControllerBase
{
    readonly IUserRepository _userRepository = userRepository;
    readonly IUserSessionManager _sessionManager = sessionManager;

    /// <summary>
    /// Get all users
    /// </summary>
    /// <param name="search">Search by name</param>
    /// <param name="pageNumber"></param>
    /// <param name="pageSize"></param>
    /// <response code="200">When there are no errors</response>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedList<UserDto>>> Index(
        string search = "",
        int pageNumber = Domain.Constants.MinPageNumber,
        int pageSize = Domain.Constants.MaxPageSize)
    {
        var users = await _userRepository.Paginate(
            search,
            pageNumber,
            pageSize);

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