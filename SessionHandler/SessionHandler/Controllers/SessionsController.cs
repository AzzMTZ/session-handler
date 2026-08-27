using Microsoft.AspNetCore.Mvc;
using SessionHandler.Interfaces;
using SessionHandler.Models;

namespace SessionHandler.Controllers;

/// <summary>
/// Ingestion and query surface for sessions.
/// <list type="bullet">
///   <item><c>POST /sessions/login</c> — apply a Login event</item>
///   <item><c>POST /sessions/update</c> — apply an Update event</item>
///   <item><c>POST /sessions/logout</c> — apply a Logout event</item>
///   <item><c>GET /sessions</c> — query active and historical sessions</item>
/// </list>
/// </summary>
[ApiController]
[Route("sessions")]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessions;

    public SessionsController(ISessionService sessions) => _sessions = sessions;

    [HttpPost]
    public async Task<ActionResult<Session>> Login([FromBody] LoginEvent loginEvent,
        CancellationToken cancellationToken)
    {
        var createdSession = await _sessions.Login(loginEvent, cancellationToken);
        return CreatedAtAction(nameof(Login), createdSession);
    }

    [HttpPut]
    public async Task<ActionResult<Session>> Update([FromBody] UpdateEvent @event, CancellationToken cancellationToken)
    {
        var updatedSession = await _sessions.Update(@event, cancellationToken);
        return Ok(updatedSession);
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Logout([FromBody] LogoutEvent @event, CancellationToken cancellationToken)
    {
        await _sessions.Logout(@event, cancellationToken);
        return NoContent();
    }

    [HttpPost("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<Session>> Search(
        [FromBody] SessionQuery query, CancellationToken cancellationToken)
    {
        var results = await _sessions.Query(query, cancellationToken);
        return Ok(results);
    }
}