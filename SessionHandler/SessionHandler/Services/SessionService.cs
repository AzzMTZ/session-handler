using Microsoft.EntityFrameworkCore;
using SessionHandler.Dtos;
using SessionHandler.Exceptions;
using SessionHandler.Interfaces;
using SessionHandler.Models;

namespace SessionHandler.Services;

/// <inheritdoc cref="ISessionService" />
/// <remarks>
/// Login/Update/Logout each stage a <see cref="SessionEvent"/> row alongside the
/// session mutation itself, via <paramref name="eventRepository"/>. Both repositories
/// wrap the same scoped <c>SessionDbContext</c>, so the single <c>SaveChanges</c> call
/// at the end of each method flushes both as one atomic transaction — an event is
/// never recorded without its session change committing, or vice versa.
/// </remarks>
public class SessionService(ISessionRepository repository, ISessionEventRepository eventRepository) : ISessionService
{
    public async Task<Session> Login(LoginEvent loginEvent, CancellationToken cancellationToken = default)
    {
        var timestamp = AsUtc(loginEvent.Timestamp);
        var active = await repository.GetActiveByCompoundId(
            loginEvent.TenantId, loginEvent.Username, loginEvent.Ip, cancellationToken);

        if (active is not null)
        {
            throw new SessionAlreadyExistsException(loginEvent.TenantId, loginEvent.Username, loginEvent.Ip);
        }

        active = await repository.Add(new Session
        {
            TenantId = loginEvent.TenantId,
            Username = loginEvent.Username,
            Ip = loginEvent.Ip,
            Tags = loginEvent.Tags.ToList(),
            LoginAt = timestamp,
            LastSeenAt = timestamp,
        }, cancellationToken);

        // Session.Id isn't assigned until SaveChanges runs, so the event links to it via
        // the tracked Session reference rather than a not-yet-known SessionId — EF Core's
        // change tracker fixes up the foreign key once the insert order is resolved.
        await eventRepository.Add(new SessionEvent
        {
            Session = active,
            TenantId = loginEvent.TenantId,
            Username = loginEvent.Username,
            Ip = loginEvent.Ip,
            Tags = loginEvent.Tags.ToList(),
            Timestamp = timestamp,
            Type = SessionEventType.Login,
        }, cancellationToken);

        await repository.SaveChanges(cancellationToken);
        return active;
    }

    public async Task<Session> Update(UpdateEvent updateEvent, CancellationToken cancellationToken = default)
    {
        var active = await repository.GetActiveByCompoundId(
            updateEvent.TenantId, updateEvent.Username, updateEvent.Ip, cancellationToken);

        if (active is null)
        {
            throw new SessionNotFoundException(updateEvent.TenantId, updateEvent.Username, updateEvent.Ip);
        }

        var timestamp = AsUtc(updateEvent.Timestamp);
        active.Tags = updateEvent.Tags.ToList();
        UpdateLastSeenAt(active, timestamp);

        await eventRepository.Add(new SessionEvent
        {
            Session = active,
            TenantId = updateEvent.TenantId,
            Username = updateEvent.Username,
            Ip = updateEvent.Ip,
            Tags = updateEvent.Tags.ToList(),
            Timestamp = timestamp,
            Type = SessionEventType.Update,
        }, cancellationToken);

        await repository.SaveChanges(cancellationToken);
        return active;
    }

    public async Task Logout(LogoutEvent logoutEvent, CancellationToken cancellationToken = default)
    {
        var timestamp = AsUtc(logoutEvent.Timestamp);
        var active = await repository.GetActiveByCompoundId(
            logoutEvent.TenantId, logoutEvent.Username, logoutEvent.Ip, cancellationToken);

        if (active is null)
        {
            throw new SessionNotFoundException(logoutEvent.TenantId, logoutEvent.Username, logoutEvent.Ip);
        }

        active.LogoutAt = timestamp;
        UpdateLastSeenAt(active, timestamp);

        // No Tags: Logout doesn't carry them, and the session's tags at close are
        // already on the preceding Login/Update events for anyone who needs them.
        await eventRepository.Add(new SessionEvent
        {
            Session = active,
            TenantId = logoutEvent.TenantId,
            Username = logoutEvent.Username,
            Ip = logoutEvent.Ip,
            Tags = null,
            Timestamp = timestamp,
            Type = SessionEventType.Logout,
        }, cancellationToken);

        await repository.SaveChanges(cancellationToken);
    }

    public async Task<Session> GetById(int id, CancellationToken cancellationToken = default)
    {
        var session = await repository.GetById(id, cancellationToken);

        if (session is null)
        {
            throw new SessionNotFoundException(id);
        }

        return session;
    }

    public async Task<List<Session>> Search(
        SessionQuery query, CancellationToken cancellationToken = default)
    {
        var sessions = repository.Query();

        if (!string.IsNullOrEmpty(query.TenantId))
        {
            sessions = sessions.Where(s => s.TenantId == query.TenantId);
        }

        if (!string.IsNullOrEmpty(query.Username))
        {
            sessions = sessions.Where(s => s.Username == query.Username);
        }

        if (!string.IsNullOrEmpty(query.Ip))
        {
            sessions = sessions.Where(s => s.Ip == query.Ip);
        }

        if (query.Tags is not null && query.Tags.Count > 0)
        {
            sessions = query.Tags.Where(queryTag => !string.IsNullOrEmpty(queryTag))
                .Aggregate(sessions,
                    (current, queryTag) =>
                        current.Where(session => session.Tags.Contains(queryTag))
                );
        }

        // Active-only unless the caller explicitly opts out with ActiveOnly: false.
        if (query.ActiveOnly != false)
        {
            sessions = sessions.Where(s => s.LogoutAt == null);
        }

        if (query.LoginAt is { Since: { } loginSince })
        {
            var since = AsUtc(loginSince);
            sessions = sessions.Where(s => s.LoginAt >= since);
        }

        if (query.LoginAt is { Until: { } loginUntil })
        {
            var until = AsUtc(loginUntil);
            sessions = sessions.Where(s => s.LoginAt <= until);
        }

        if (query.LogoutAt is { Since: { } logoutSince })
        {
            var since = AsUtc(logoutSince);
            sessions = sessions.Where(s => s.LogoutAt != null && s.LogoutAt >= since);
        }

        if (query.LogoutAt is { Until: { } logoutUntil })
        {
            var until = AsUtc(logoutUntil);
            sessions = sessions.Where(s => s.LogoutAt != null && s.LogoutAt <= until);
        }

        if (query.LastSeenAt is { Since: { } updateSince })
        {
            var since = AsUtc(updateSince);
            sessions = sessions.Where(s => s.LastSeenAt >= since);
        }

        if (query.LastSeenAt is { Until: { } updateUntil })
        {
            var until = AsUtc(updateUntil);
            sessions = sessions.Where(s => s.LastSeenAt <= until);
        }

        var results = await sessions
            .OrderByDescending(s => s.LoginAt)
            .ToListAsync(cancellationToken);

        return results;
    }

    /// <summary>
    /// Advances the session's last-seen timestamp. The value only ever moves forward,
    /// so an out-of-order event cannot rewind it.
    /// </summary>
    private static void UpdateLastSeenAt(Session session, DateTime timestamp)
    {
        if (timestamp > session.LastSeenAt)
        {
            session.LastSeenAt = timestamp;
        }
    }

    /// <summary>
    /// Normalizes an inbound timestamp to UTC so all stored values are directly comparable.
    /// A value with no kind is assumed to already be UTC.
    /// </summary>
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}