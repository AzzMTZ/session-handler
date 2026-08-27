namespace SessionHandler.Dtos;

/// <summary>Attributes of an existing session changed (e.g. tags added/removed) while connected.</summary>
public record UpdateEvent(
    string TenantId,
    string Username,
    string Ip,
    IReadOnlyList<string> Tags,
    DateTime Timestamp);
