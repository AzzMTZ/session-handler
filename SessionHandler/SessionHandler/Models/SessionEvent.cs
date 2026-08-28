namespace SessionHandler.Models;

/// <summary>
/// An immutable record of a single Login/Update/Logout occurrence — the audit trail
/// behind a <see cref="Session"/>'s current state. Never mutated or deleted.
/// </summary>
public class SessionEvent
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the owning <see cref="Session"/> — pins the event to one session
    /// instance, which the identity triple can't do across repeated logins.
    /// </summary>
    public int SessionId { get; set; }

    /// <summary>
    /// Navigation to the owning session. Lets EF Core resolve <see cref="SessionId"/>
    /// when the event is inserted alongside a brand-new session in the same unit of
    /// work; not eagerly loaded on reads.
    /// </summary>
    public Session? Session { get; set; }

    /// <summary>Identifies the tenant (customer / organization).</summary>
    public string TenantId { get; set; } = null!;

    /// <summary>Identifies the user within a tenant.</summary>
    public string Username { get; set; } = null!;

    public string Ip { get; set; } = null!;

    /// <summary>
    /// The session's tags as of this event. <c>null</c> for <see cref="SessionEventType.Logout"/>,
    /// which carries no tags; always populated for Login/Update.
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>When this event occurred (UTC), as reported by the caller.</summary>
    public DateTime Timestamp { get; set; }

    public SessionEventType Type { get; set; }
}
