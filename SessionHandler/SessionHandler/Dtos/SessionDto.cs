using SessionHandler.Models;

namespace SessionHandler.Dtos;

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
