namespace SessionHandler.Dtos;

/// <summary>A user established a session from <see cref="Ip"/> with these <see cref="Tags"/>.</summary>
public record LoginEvent(
    string TenantId,
    string Username,
    string Ip,
    IReadOnlyList<string> Tags,
    DateTime Timestamp);
