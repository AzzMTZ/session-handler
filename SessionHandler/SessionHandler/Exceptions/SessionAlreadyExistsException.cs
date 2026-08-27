namespace SessionHandler.Exceptions;

/// <summary>
/// Thrown when a Login event would open a session for the identity triple
/// <c>(TenantId, Username, Ip)</c> that already has one active.
/// </summary>
public class SessionAlreadyExistsException : Exception
{
    public SessionAlreadyExistsException(string tenantId, string username, string ip)
        : base($"An active session already exists for tenant '{tenantId}', user '{username}' from IP '{ip}'.")
    {
        TenantId = tenantId;
        Username = username;
        Ip = ip;
    }

    public string TenantId { get; }

    public string Username { get; }

    public string Ip { get; }
}
