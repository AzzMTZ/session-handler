namespace SessionHandler.Exceptions;

/// <summary>
/// Thrown when an operation targets an active session for the identity triple
/// <c>(TenantId, Username, Ip)</c> but no such session exists.
/// </summary>
public class SessionNotFoundException : Exception
{
    public SessionNotFoundException(string tenantId, string username, string ip)
        : base($"No active session found for tenant '{tenantId}', user '{username}' from IP '{ip}'.")
    {
        TenantId = tenantId;
        Username = username;
        Ip = ip;
    }

    /// <summary>Thrown when a lookup by surrogate id matches no session, active or historical.</summary>
    public SessionNotFoundException(int id)
        : base($"No session found with id '{id}'.")
    {
        Id = id;
    }

    public string? TenantId { get; }

    public string? Username { get; }

    public string? Ip { get; }

    public int? Id { get; }
}
