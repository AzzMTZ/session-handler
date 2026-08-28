using Microsoft.AspNetCore.Mvc;
using SessionHandler.Dtos;
using SessionHandler.Interfaces;

namespace SessionHandler.Controllers;

/// <summary>
/// Read-only query surface over the recorded Login/Update/Logout events. Events
/// themselves are written by <c>SessionsController</c>'s Login/Update/Logout actions
/// (via <see cref="ISessionService"/>), not through this controller.
/// <list type="bullet">
///   <item><c>GET /session-events/{id}</c> — fetch a single event by id</item>
///   <item><c>POST /session-events/search</c> — query events on any attribute or combination</item>
/// </list>
/// </summary>
[ApiController]
[Route("session-events")]
public class SessionEventsController(ISessionEventService sessionEventsService) : ControllerBase
{
    [HttpGet("{id:int}")]
    [ProducesResponseType<SessionEventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SessionEventResponse>> Get(int id, CancellationToken cancellationToken)
    {
        SessionEventResponse sessionEvent = await sessionEventsService.GetById(id, cancellationToken);
        return Ok(sessionEvent);
    }

    [HttpPost("search")]
    [ProducesResponseType<List<SessionEventResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<SessionEventResponse>>> Search(
        [FromBody] SessionEventQuery query, CancellationToken cancellationToken)
    {
        var results = await sessionEventsService.Search(query, cancellationToken);
        return Ok(results.ConvertAll<SessionEventResponse>(sessionEvent => sessionEvent));
    }
}
