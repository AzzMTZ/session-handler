namespace SessionHandler.Models;

/// <summary>A user established a session from <see cref="Ip"/> with these <see cref="Tags"/>.</summary>
public record LoginEvent(
    string TenantId,
    string Username,
    string Ip,
    IReadOnlyList<string> Tags,
    DateTime Timestamp);

/// <summary>Attributes of an existing session changed (e.g. tags added/removed) while connected.</summary>
public record UpdateEvent(
    string TenantId,
    string Username,
    string Ip,
    IReadOnlyList<string> Tags,
    DateTime Timestamp);

/// <summary>The session for this exact <c>(TenantId, Username, Ip)</c> has ended.</summary>
public record LogoutEvent(
    string TenantId,
    string Username,
    string Ip,
    DateTime Timestamp);

/// <summary>
/// Filter for querying sessions. All members are optional and AND-combined; an empty
/// query returns every session (active and historical).
/// </summary>
public record SessionQuery
{
    public string? TenantId { get; init; }
    public string? Username { get; init; }
    public string? Ip { get; init; }

    /// <summary>Match sessions that carry this exact tag.</summary>
    public string? Tag { get; init; }

    /// <summary>When true, restrict to sessions that have not logged out.</summary>
    public bool? ActiveOnly { get; init; }

    /// <summary>
    /// Point-in-time filter (UTC): sessions that were open at this instant
    /// (<c>LoginAt &lt;= ActiveAt &lt; LogoutAt</c>, treating a null logout as "still open").
    /// </summary>
    public DateTime? ActiveAt { get; init; }

    /// <summary>Restrict to sessions whose login happened at or after this instant (UTC).</summary>
    public DateTime? LoggedInAtOrAfter { get; init; }
}

/// <summary>Read model returned by the query endpoint.</summary>
public record SessionDto(
    int Id,
    string TenantId,
    string Username,
    string Ip,
    IReadOnlyList<string> Tags,
    DateTime LoginAt,
    DateTime LastSeenAt,
    DateTime? LogoutAt)
{
    public bool IsActive => LogoutAt is null;

    public static SessionDto From(Session session) => new(
        session.Id,
        session.TenantId,
        session.Username,
        session.Ip,
        session.Tags,
        AsUtc(session.LoginAt),
        AsUtc(session.LastSeenAt),
        session.LogoutAt is { } logoutAt ? AsUtc(logoutAt) : null);

    // SQLite round-trips DateTime as text without a kind; stamp it back to UTC so the
    // serialized response carries a 'Z'.
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
