using Microsoft.AspNetCore.Mvc;
using SessionHandler.Dtos;
using SessionHandler.Interfaces;

namespace SessionHandler.Controllers;

/// <summary>
/// Ingestion and query surface for sessions.
/// <list type="bullet">
///   <item><c>POST /sessions</c> — apply a Login event</item>
///   <item><c>GET /sessions/{id}</c> — fetch a session (active or historical) by its surrogate id</item>
///   <item><c>PUT /sessions/{tenantId}/{username}/{ip}</c> — apply an Update event</item>
///   <item><c>DELETE /sessions/{tenantId}/{username}/{ip}</c> — apply a Logout event</item>
///   <item><c>POST /sessions/search</c> — query active and historical sessions</item>
/// </list>
/// </summary>
[ApiController]
[Route("sessions")]
public class SessionsController(ISessionService sessionsService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SessionResponse>> Login([FromBody] LoginEvent loginEvent,
        CancellationToken cancellationToken)
    {
        SessionResponse createdSession = await sessionsService.Login(loginEvent, cancellationToken);
        return CreatedAtAction(
            nameof(GetById),
            new
            {
                id = createdSession.Id
            },
            createdSession);
    }

    /// <summary>
    /// Looks up a session by its surrogate id (as returned in <see cref="SessionResponse.Id"/>),
    /// active or historical. There is no lookup by the identity triple alone — Update and
    /// Logout still address a session that way since it is always the active one, but a GET
    /// has no such guarantee, so the id is the only unambiguous key for a single-session fetch.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        SessionResponse session = await sessionsService.GetById(id, cancellationToken);
        return Ok(session);
    }

    [HttpPut("{tenantId}/{username}/{ip}")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionResponse>> Update(
        string tenantId, string username, string ip,
        [FromBody] UpdateSessionRequest request, CancellationToken cancellationToken)
    {
        var updateEvent = new UpdateEvent(tenantId, username, ip, request.Tags, request.Timestamp);
        SessionResponse updatedSession = await sessionsService.Update(updateEvent, cancellationToken);
        return Ok(updatedSession);
    }

    [HttpDelete("{tenantId}/{username}/{ip}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Logout(
        string tenantId, string username, string ip,
        [FromBody] LogoutSessionRequest request, CancellationToken cancellationToken)
    {
        var logoutEvent = new LogoutEvent(tenantId, username, ip, request.Timestamp);
        await sessionsService.Logout(logoutEvent, cancellationToken);
        return NoContent();
    }

    [HttpPost("search")]
    [ProducesResponseType<List<SessionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<SessionResponse>>> Search(
        [FromBody] SessionQuery query, CancellationToken cancellationToken)
    {
        var results = await sessionsService.Search(query, cancellationToken);
        return Ok(results.ConvertAll<SessionResponse>(session => session));
    }
}