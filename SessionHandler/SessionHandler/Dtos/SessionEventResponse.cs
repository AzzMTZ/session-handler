using SessionHandler.Models;
using SessionHandler.Utils;

namespace SessionHandler.Dtos;

/// <summary>Read model returned by the session event endpoints.</summary>
public record SessionEventResponse(
    int Id,
    int SessionId,
    string TenantId,
    string Username,
    string Ip,
    IReadOnlyList<string>? Tags,
    DateTime Timestamp,
    SessionEventType Type)
{
    public static implicit operator SessionEventResponse(SessionEvent sessionEvent) => new(
        sessionEvent.Id,
        sessionEvent.SessionId,
        sessionEvent.TenantId,
        sessionEvent.Username,
        sessionEvent.Ip,
        sessionEvent.Tags,
        sessionEvent.Timestamp.AsUtc(),
        sessionEvent.Type);
}
