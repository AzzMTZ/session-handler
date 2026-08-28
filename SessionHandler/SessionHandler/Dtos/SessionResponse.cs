using SessionHandler.Models;

namespace SessionHandler.Dtos;

/// <summary>Read model returned by the session endpoints.</summary>
public record SessionResponse(
    int Id,
    string TenantId,
    string Username,
    string Ip,
    IReadOnlyList<string> Tags,
    DateTime LoginAt,
    DateTime LastSeenAt,
    DateTime? LogoutAt)
{
    public static implicit operator SessionResponse(Session session) => new(
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