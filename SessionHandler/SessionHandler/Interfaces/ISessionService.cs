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

    Task<List<Session>> Search(SessionQuery query, CancellationToken cancellationToken = default);
}