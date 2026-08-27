namespace SessionHandler.Dtos;

/// <summary>
/// Filter for querying sessions. All members are optional and AND-combined; an empty
/// query returns every session (active and historical).
/// </summary>
public record SessionQuery
{
    public string? TenantId { get; init; }
    public string? Username { get; init; }
    public string? Ip { get; init; }
    public List<string>? Tags { get; init; }
    public bool? ActiveOnly { get; init; }
    public DateRange? LoginAt { get; init; }
    public DateRange? LogoutAt { get; init; }
    public DateRange? LastSeenAt { get; init; }
}