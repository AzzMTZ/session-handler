using Microsoft.EntityFrameworkCore;
using SessionHandler.Interfaces;
using SessionHandler.Models;

namespace SessionHandler.Services;

/// <inheritdoc cref="ISessionService" />
public class SessionService : ISessionService
{
    private readonly ISessionRepository _repository;

    public SessionService(ISessionRepository repository) => _repository = repository;

    public async Task<Session> Login(LoginEvent @event, CancellationToken cancellationToken = default)
    {
        var timestamp = AsUtc(@event.Timestamp);
        var active = await _repository.GetActiveAsync(
            @event.TenantId, @event.Username, @event.Ip, cancellationToken);

        if (active is null)
        {
            active = await _repository.Add(new Session
            {
                TenantId = @event.TenantId,
                Username = @event.Username,
                Ip = @event.Ip,
                Tags = @event.Tags.ToList(),
                LoginAt = timestamp,
                LastSeenAt = timestamp,
            }, cancellationToken);
        }
        else
        {
            // Re-login for an already-open triple: refresh attributes, don't duplicate.
            ApplyAttributes(active, @event.Tags, timestamp);
        }

        await _repository.SaveChanges(cancellationToken);
        return active;
    }

    public async Task<Session> Update(UpdateEvent @event, CancellationToken cancellationToken = default)
    {
        var active = await _repository.GetActiveAsync(
            @event.TenantId, @event.Username, @event.Ip, cancellationToken);

        if (active is null)
        {
            throw new FileNotFoundException();
        }

        ApplyAttributes(active, @event.Tags, AsUtc(@event.Timestamp));
        await _repository.SaveChanges(cancellationToken);

        return active;
    }

    public async Task<Session> Logout(LogoutEvent @event, CancellationToken cancellationToken = default)
    {
        var timestamp = AsUtc(@event.Timestamp);
        var active = await _repository.GetActiveAsync(
            @event.TenantId, @event.Username, @event.Ip, cancellationToken);

        if (active is null)
        {
            throw new FileNotFoundException();
        }

        active.LogoutAt = timestamp;
        if (timestamp > active.LastSeenAt)
        {
            active.LastSeenAt = timestamp;
        }

        await _repository.SaveChanges(cancellationToken);
        return active;
    }

    public async Task<List<Session>> Query(
        SessionQuery query, CancellationToken cancellationToken = default)
    {
        var sessions = _repository.Query();

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

        if (!string.IsNullOrEmpty(query.Tag))
        {
            sessions = sessions.Where(s => s.Tags.Contains(query.Tag));
        }

        if (query.ActiveOnly == true)
        {
            sessions = sessions.Where(s => s.LogoutAt == null);
        }

        if (query.ActiveAt is { } activeAtInput)
        {
            var activeAt = AsUtc(activeAtInput);
            sessions = sessions.Where(s =>
                s.LoginAt <= activeAt && (s.LogoutAt == null || s.LogoutAt > activeAt));
        }

        if (query.LoggedInAtOrAfter is { } fromInput)
        {
            var from = AsUtc(fromInput);
            sessions = sessions.Where(s => s.LoginAt >= from);
        }

        var results = await sessions
            .OrderByDescending(s => s.LoginAt)
            .ToListAsync(cancellationToken);

        return results;
    }

    /// <summary>
    /// Overwrites the mutable attributes of a session. The last-seen timestamp only ever
    /// moves forward, so an out-of-order event cannot rewind it.
    /// </summary>
    private static void ApplyAttributes(Session session, IReadOnlyList<string> tags, DateTime timestamp)
    {
        session.Tags = [.. tags];
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