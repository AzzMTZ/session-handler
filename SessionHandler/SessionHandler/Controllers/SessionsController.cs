using Microsoft.AspNetCore.Mvc;
using SessionHandler.Dtos;
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
public class SessionsController(ISessionService sessionsService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Session>> Login([FromBody] LoginEvent loginEvent,
        CancellationToken cancellationToken)
    {
        var createdSession = await sessionsService.Login(loginEvent, cancellationToken);
        return CreatedAtAction(nameof(Login), createdSession);
    }

    [HttpPut]
    public async Task<ActionResult<Session>> Update([FromBody] UpdateEvent updateEvent, CancellationToken cancellationToken)
    {
        var updatedSession = await sessionsService.Update(updateEvent, cancellationToken);
        return Ok(updatedSession);
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutEvent logoutEvent, CancellationToken cancellationToken)
    {
        await sessionsService.Logout(logoutEvent, cancellationToken);
        return NoContent();
    }

    [HttpPost("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Session>>> Search(
        [FromBody] SessionQuery query, CancellationToken cancellationToken)
    {
        var results = await sessionsService.Search(query, cancellationToken);
        return Ok(results);
    }
}