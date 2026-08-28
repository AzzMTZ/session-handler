namespace SessionHandler.Models;

/// <summary>
/// A single connection for an identity <c>(TenantId, Username)</c> from one IP address.
/// The same identity may hold several concurrent sessions from different IPs, so the
/// natural key of an <em>active</em> session is the triple <c>(TenantId, Username, Ip)</c>.
/// Rows are kept after logout to support point-in-time queries, so the table can hold
/// many historical sessions for the same triple.
/// </summary>
public class Session
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>Identifies the tenant (customer / organization).</summary>
    public string TenantId { get; set; } = null!;

    /// <summary>Identifies the user within a tenant.</summary>
    public string Username { get; set; } = null!;

    public string Ip { get; set; } = null!;

    /// <summary>
    /// Free-form labels (groups, roles, device info). EF Core maps this primitive
    /// collection to a JSON column on SQLite, no extra configuration required.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>When the Login event that opened this session occurred (UTC).</summary>
    public DateTime LoginAt { get; set; }

    /// <summary>Timestamp of the most recent Login/Update event applied to this session (UTC).</summary>
    public DateTime LastSeenAt { get; set; }

    /// <summary>
    /// <c>null</c> while the session is active; set to the Logout event timestamp (UTC)
    /// once the session has ended.
    /// </summary>
    public DateTime? LogoutAt { get; set; }
}
