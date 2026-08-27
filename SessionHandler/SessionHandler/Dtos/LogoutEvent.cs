namespace SessionHandler.Dtos;

/// <summary>The session for this exact <c>(TenantId, Username, Ip)</c> has ended.</summary>
public record LogoutEvent(
    string TenantId,
    string Username,
    string Ip,
    DateTime Timestamp);
