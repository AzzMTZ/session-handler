using SessionHandler.Models;

namespace SessionHandler.Dtos;

/// <summary>
/// Filter for querying session events. All members are optional and AND-combined; an
/// empty query returns every event ever recorded.
/// </summary>
public record SessionEventQuery
{
    /// <summary>
    /// Scopes to one exact session instance (from <see cref="SessionResponse.Id"/>).
    /// Prefer this over <see cref="TenantId"/>/<see cref="Username"/>/<see cref="Ip"/>
    /// when the question is about one specific session's history, since the identity
    /// triple alone can match several sessions across time.
    /// </summary>
    public int? SessionId { get; init; }

    public string? TenantId { get; init; }
    public string? Username { get; init; }
    public string? Ip { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public SessionEventType? Type { get; init; }
    public DateRange? Timestamp { get; init; }
}
