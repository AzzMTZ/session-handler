using SessionHandler.Models;

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
        AsUtc(sessionEvent.Timestamp),
        sessionEvent.Type);

    // SQLite round-trips DateTime as text without a kind; stamp it back to UTC so the
    // serialized response carries a 'Z'.
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
