using Microsoft.AspNetCore.Mvc;
using SessionHandler.Dtos;
using SessionHandler.Interfaces;

namespace SessionHandler.Controllers;

/// <summary>
/// Ingestion and query surface for sessions.
/// <list type="bullet">
///   <item><c>POST /sessions</c> — apply a Login event</item>
///   <item><c>GET /sessions/{tenantId}/{username}/{ip}</c> — fetch the active session for an identity</item>
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
    public async Task<ActionResult<SessionResponse>> Login([FromBody] LoginEvent loginEvent,
        CancellationToken cancellationToken)
    {
        SessionResponse createdSession = await sessionsService.Login(loginEvent, cancellationToken);
        return CreatedAtAction(
            nameof(Get),
            new
            {
                tenantId = createdSession.TenantId,
                username = createdSession.Username,
                ip = createdSession.Ip,
            },
            createdSession);
    }

    [HttpGet("{tenantId}/{username}/{ip}")]
    public async Task<ActionResult<SessionResponse>> Get(
        string tenantId, string username, string ip, CancellationToken cancellationToken)
    {
        SessionResponse session = await sessionsService.Get(tenantId, username, ip, cancellationToken);
        return Ok(session);
    }

    [HttpPut("{tenantId}/{username}/{ip}")]
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
    public async Task<IActionResult> Logout(
        string tenantId, string username, string ip,
        [FromBody] LogoutSessionRequest request, CancellationToken cancellationToken)
    {
        var logoutEvent = new LogoutEvent(tenantId, username, ip, request.Timestamp);
        await sessionsService.Logout(logoutEvent, cancellationToken);
        return NoContent();
    }

    [HttpPost("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SessionResponse>>> Search(
        [FromBody] SessionQuery query, CancellationToken cancellationToken)
    {
        var results = await sessionsService.Search(query, cancellationToken);
        return Ok(results.ConvertAll<SessionResponse>(session => session));
    }
}