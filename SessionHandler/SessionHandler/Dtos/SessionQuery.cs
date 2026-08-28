namespace SessionHandler.Dtos;

/// <summary>
/// Filter for querying sessions. All members are optional and AND-combined. Note the
/// filter is not neutral when empty: <see cref="ActiveOnly"/> defaults to active-only,
/// so an otherwise-empty query returns only sessions that are still open — pass
/// <c>ActiveOnly: false</c> to include historical (logged-out) ones.
/// </summary>
public record SessionQuery
{
    public string? TenantId { get; init; }
    public string? Username { get; init; }
    public string? Ip { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// <c>null</c> (the default) and <c>true</c> both restrict results to active
    /// sessions (<c>LogoutAt is null</c>); only an explicit <c>false</c> widens the
    /// query to active and historical sessions alike.
    /// </summary>
    public bool? ActiveOnly { get; init; }
    public DateRange? LoginAt { get; init; }
    public DateRange? LogoutAt { get; init; }
    public DateRange? LastSeenAt { get; init; }
}