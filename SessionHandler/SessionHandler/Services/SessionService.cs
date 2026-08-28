using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SessionHandler.Dtos;
using SessionHandler.Exceptions;
using SessionHandler.Interfaces;
using SessionHandler.Models;
using SessionHandler.Utils;

namespace SessionHandler.Services;

/// <inheritdoc cref="ISessionService" />
/// <remarks>
/// Login/Update/Logout each stage a <see cref="SessionEvent"/> next to the session
/// mutation and commit both through the one <paramref name="unitOfWork"/>, so they
/// land as a single transaction. Each also holds <paramref name="locks"/> on the
/// identity triple across its whole read-decide-write, so concurrent requests for the
/// same <c>(TenantId, Username, Ip)</c> can't race (double-insert on Login, lost
/// update on Update). Single-process only; the partial unique index behind
/// <see cref="SessionAlreadyExistsException"/> is the database-level backstop.
/// </remarks>
public class SessionService(
    ISessionRepository repository,
    ISessionEventRepository eventRepository,
    IUnitOfWork unitOfWork,
    KeyedAsyncLock<(string TenantId, string Username, string Ip)> locks)
    : ISessionService
{
    public async Task<Session> Login(LoginEvent loginEvent, CancellationToken cancellationToken = default)
    {
        using var _ = await locks.LockAsync(
            (loginEvent.TenantId, loginEvent.Username, loginEvent.Ip), cancellationToken);

        var timestamp = loginEvent.Timestamp.AsUtc();
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

        // Link via the tracked Session reference, not SessionId: the id isn't assigned
        // until SaveChanges, and EF Core fixes up the FK on insert.
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

        try
        {
            await unitOfWork.SaveChanges(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            // 2067 = SQLITE_CONSTRAINT_UNIQUE on the partial unique index. The lock above
            // should make this unreachable; it's a fallback for writes that bypass it.
            throw new SessionAlreadyExistsException(loginEvent.TenantId, loginEvent.Username, loginEvent.Ip);
        }

        return active;
    }

    public async Task<Session> Update(UpdateEvent updateEvent, CancellationToken cancellationToken = default)
    {
        using var _ = await locks.LockAsync(
            (updateEvent.TenantId, updateEvent.Username, updateEvent.Ip), cancellationToken);

        var active = await repository.GetActiveByCompoundId(
            updateEvent.TenantId, updateEvent.Username, updateEvent.Ip, cancellationToken);

        if (active is null)
        {
            throw new SessionNotFoundException(updateEvent.TenantId, updateEvent.Username, updateEvent.Ip);
        }

        var timestamp = updateEvent.Timestamp.AsUtc();

        // Only fold an in-order event into current state; the event row itself is
        // always recorded below regardless of arrival order.
        if (timestamp >= active.LastSeenAt)
        {
            active.Tags = updateEvent.Tags.ToList();
            active.LastSeenAt = timestamp;
        }

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

        await unitOfWork.SaveChanges(cancellationToken);
        return active;
    }

    public async Task Logout(LogoutEvent logoutEvent, CancellationToken cancellationToken = default)
    {
        using var _ = await locks.LockAsync(
            (logoutEvent.TenantId, logoutEvent.Username, logoutEvent.Ip), cancellationToken);

        var timestamp = logoutEvent.Timestamp.AsUtc();
        var active = await repository.GetActiveByCompoundId(
            logoutEvent.TenantId, logoutEvent.Username, logoutEvent.Ip, cancellationToken);

        if (active is null)
        {
            throw new SessionNotFoundException(logoutEvent.TenantId, logoutEvent.Username, logoutEvent.Ip);
        }

        // Logout always closes the session, even out of order — a terminal transition,
        // otherwise the session could stay open forever. LastSeenAt still only advances.
        active.LogoutAt = timestamp;
        if (timestamp > active.LastSeenAt)
        {
            active.LastSeenAt = timestamp;
        }

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

        await unitOfWork.SaveChanges(cancellationToken);
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
            var since = loginSince.AsUtc();
            sessions = sessions.Where(s => s.LoginAt >= since);
        }

        if (query.LoginAt is { Until: { } loginUntil })
        {
            var until = loginUntil.AsUtc();
            sessions = sessions.Where(s => s.LoginAt <= until);
        }

        if (query.LogoutAt is { Since: { } logoutSince })
        {
            var since = logoutSince.AsUtc();
            sessions = sessions.Where(s => s.LogoutAt != null && s.LogoutAt >= since);
        }

        if (query.LogoutAt is { Until: { } logoutUntil })
        {
            var until = logoutUntil.AsUtc();
            sessions = sessions.Where(s => s.LogoutAt != null && s.LogoutAt <= until);
        }

        if (query.LastSeenAt is { Since: { } updateSince })
        {
            var since = updateSince.AsUtc();
            sessions = sessions.Where(s => s.LastSeenAt >= since);
        }

        if (query.LastSeenAt is { Until: { } updateUntil })
        {
            var until = updateUntil.AsUtc();
            sessions = sessions.Where(s => s.LastSeenAt <= until);
        }

        var results = await sessions
            .OrderByDescending(s => s.LoginAt)
            .ToListAsync(cancellationToken);

        return results;
    }
}
