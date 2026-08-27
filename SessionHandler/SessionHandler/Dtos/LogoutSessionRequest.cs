namespace SessionHandler.Dtos;

/// <summary>
/// Body of a <c>DELETE /sessions/{tenantId}/{username}/{ip}</c> request. The identity triple
/// travels in the route; only the Logout event timestamp rides in the payload.
/// </summary>
public record LogoutSessionRequest(DateTime Timestamp);
