namespace SessionHandler.Dtos;

/// <summary>
/// Body of a <c>PUT /sessions/{tenantId}/{username}/{ip}</c> request. The identity triple
/// travels in the route; only the mutable attributes ride in the payload.
/// </summary>
public record UpdateSessionRequest(
    IReadOnlyList<string> Tags,
    DateTime Timestamp);
