using Microsoft.EntityFrameworkCore;
using SessionHandler.Dtos;
using SessionHandler.Exceptions;
using SessionHandler.Interfaces;
using SessionHandler.Models;

namespace SessionHandler.Services;

/// <inheritdoc cref="ISessionService" />
public class SessionService(ISessionRepository repository) : ISessionService
{
    public async Task<Session> Login(LoginEvent loginEvent, CancellationToken cancellationToken = default)
    {
        var timestamp = AsUtc(loginEvent.Timestamp);
        var active = await repository.GetActiveByCompoudId(
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


        await repository.SaveChanges(cancellationToken);
        return active;
    }

    public async Task<Session> Update(UpdateEvent updateEvent, CancellationToken cancellationToken = default)
    {
        var active = await repository.GetActiveByCompoudId(
            updateEvent.TenantId, updateEvent.Username, updateEvent.Ip, cancellationToken);

        if (active is null)
        {
            throw new SessionNotFoundException(updateEvent.TenantId, updateEvent.Username, updateEvent.Ip);
        }

        active.Tags = updateEvent.Tags.ToList();
        UpdateLastSeenAt(active, AsUtc(updateEvent.Timestamp));
        await repository.SaveChanges(cancellationToken);

        return active;
    }

    public async Task Logout(LogoutEvent logoutEvent, CancellationToken cancellationToken = default)
    {
        var timestamp = AsUtc(logoutEvent.Timestamp);
        var active = await repository.GetActiveByCompoudId(
            logoutEvent.TenantId, logoutEvent.Username, logoutEvent.Ip, cancellationToken);

        if (active is null)
        {
            throw new SessionNotFoundException(logoutEvent.TenantId, logoutEvent.Username, logoutEvent.Ip);
        }

        active.LogoutAt = timestamp;
        UpdateLastSeenAt(active, timestamp);
        await repository.SaveChanges(cancellationToken);
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

        if (query.ActiveOnly == true)
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