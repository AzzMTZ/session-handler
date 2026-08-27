using Microsoft.AspNetCore.Mvc;
using SessionHandler.Dtos;
using SessionHandler.Interfaces;

namespace SessionHandler.Controllers;

/// <summary>
/// Ingestion and query surface for sessions.
/// <list type="bullet">
///   <item><c>POST /sessions</c> — apply a Login event</item>
///   <item><c>PUT /sessions</c> — apply an Update event</item>
///   <item><c>DELETE /sessions</c> — apply a Logout event</item>
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
        return CreatedAtAction(nameof(Login), createdSession);
    }

    [HttpPut]
    public async Task<ActionResult<SessionResponse>> Update([FromBody] UpdateEvent updateEvent, CancellationToken cancellationToken)
    {
        SessionResponse updatedSession = await sessionsService.Update(updateEvent, cancellationToken);
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
    public async Task<ActionResult<List<SessionResponse>>> Search(
        [FromBody] SessionQuery query, CancellationToken cancellationToken)
    {
        var results = await sessionsService.Search(query, cancellationToken);
        return Ok(results.ConvertAll<SessionResponse>(session => session));
    }
}