namespace SessionHandler.Models;

/// <summary>
/// An immutable record of a single Login/Update/Logout occurrence. Rows are never
/// mutated or deleted after insertion — they are the audit trail behind a
/// <see cref="Session"/>'s current state, and the only source for "what changed,
/// and when" queries that a session's merged current state cannot answer.
/// </summary>
public class SessionEvent
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the <see cref="Session"/> this event belongs to. Scopes an event
    /// to one exact session instance, since the identity triple alone is ambiguous
    /// across repeated logins from the same <c>(TenantId, Username, Ip)</c>.
    /// </summary>
    public int SessionId { get; set; }

    /// <summary>
    /// Navigation to the owning session. Only needed so EF Core can resolve
    /// <see cref="SessionId"/> for a session inserted in the same unit of work
    /// (e.g. the Login event alongside its brand-new session, before the session's
    /// generated key would otherwise be known); not eagerly loaded on reads.
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
