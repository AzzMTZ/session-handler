using SessionHandler.Dtos;
using SessionHandler.Models;

namespace SessionHandler.Interfaces;

/// <summary>
/// Read-only query surface over recorded session events. Events are written only as a
/// side effect of <see cref="ISessionService"/>'s Login/Update/Logout, never here.
/// </summary>
public interface ISessionEventService
{
    /// <summary>
    /// Returns the session event with the given id, or throws
    /// <see cref="Exceptions.SessionEventNotFoundException"/> if none exists.
    /// </summary>
    Task<SessionEvent> GetById(int id, CancellationToken cancellationToken = default);

    Task<List<SessionEvent>> Search(SessionEventQuery query, CancellationToken cancellationToken = default);
}
