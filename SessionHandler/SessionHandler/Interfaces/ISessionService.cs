using SessionHandler.Dtos;
using SessionHandler.Models;

namespace SessionHandler.Interfaces;

/// <summary>
/// Applies the session lifecycle events (Login / Update / Logout) and answers queries
/// over the resulting data. Sits between the controller and <see cref="ISessionRepository"/>.
/// </summary>
public interface ISessionService
{
    Task<Session> Login(LoginEvent loginEvent, CancellationToken cancellationToken = default);

    Task<Session> Update(UpdateEvent updateEvent, CancellationToken cancellationToken = default);

    Task Logout(LogoutEvent loginEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the active session for the identity triple <c>(tenantId, username, ip)</c>,
    /// or throws <see cref="Exceptions.SessionNotFoundException"/> if none is open.
    /// </summary>
    Task<Session> Get(string tenantId, string username, string ip, CancellationToken cancellationToken = default);

    Task<List<Session>> Search(SessionQuery query, CancellationToken cancellationToken = default);
}