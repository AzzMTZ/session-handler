using Microsoft.EntityFrameworkCore;
using SessionHandler.Dtos;
using SessionHandler.Exceptions;
using SessionHandler.Interfaces;
using SessionHandler.Models;

namespace SessionHandler.Services;

/// <inheritdoc cref="ISessionEventService" />
public class SessionEventService(ISessionEventRepository repository) : ISessionEventService
{
    public async Task<SessionEvent> GetById(int id, CancellationToken cancellationToken = default)
    {
        var sessionEvent = await repository.GetById(id, cancellationToken);

        if (sessionEvent is null)
        {
            throw new SessionEventNotFoundException(id);
        }

        return sessionEvent;
    }

    public async Task<List<SessionEvent>> Search(
        SessionEventQuery query, CancellationToken cancellationToken = default)
    {
        var events = repository.Query();

        if (query.SessionId is { } sessionId)
        {
            events = events.Where(e => e.SessionId == sessionId);
        }

        if (!string.IsNullOrEmpty(query.TenantId))
        {
            events = events.Where(e => e.TenantId == query.TenantId);
        }

        if (!string.IsNullOrEmpty(query.Username))
        {
            events = events.Where(e => e.Username == query.Username);
        }

        if (!string.IsNullOrEmpty(query.Ip))
        {
            events = events.Where(e => e.Ip == query.Ip);
        }

        if (query.Type is { } type)
        {
            events = events.Where(e => e.Type == type);
        }

        if (query.Tags is not null && query.Tags.Count > 0)
        {
            events = query.Tags.Where(queryTag => !string.IsNullOrEmpty(queryTag))
                .Aggregate(events,
                    (current, queryTag) =>
                        current.Where(e => e.Tags != null && e.Tags.Contains(queryTag))
                );
        }

        if (query.Timestamp is { Since: { } since })
        {
            var sinceUtc = AsUtc(since);
            events = events.Where(e => e.Timestamp >= sinceUtc);
        }

        if (query.Timestamp is { Until: { } until })
        {
            var untilUtc = AsUtc(until);
            events = events.Where(e => e.Timestamp <= untilUtc);
        }

        return await events
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Normalizes an inbound timestamp to UTC so it is directly comparable to stored
    /// values. A value with no kind is assumed to already be UTC.
    /// </summary>
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
