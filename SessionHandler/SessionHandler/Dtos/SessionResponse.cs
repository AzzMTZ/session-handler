using SessionHandler.Models;
using SessionHandler.Utils;

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
        session.LoginAt.AsUtc(),
        session.LastSeenAt.AsUtc(),
        session.LogoutAt?.AsUtc());
}