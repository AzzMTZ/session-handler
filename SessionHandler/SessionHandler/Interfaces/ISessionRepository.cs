using SessionHandler.Models;

namespace SessionHandler.Interfaces;

/// <summary>
/// Data access layer for <see cref="Session"/> rows. Write methods stage changes on
/// the underlying context; call <see cref="SaveChangesAsync"/> once per unit of work
/// to flush them. <see cref="Query"/> exposes a composable read surface so consumers
/// can filter on any attribute (or combination, including time).
/// </summary>
public interface ISessionRepository
{
    /// <summary>Stages a new session for insertion.</summary>
    Task<Session> Add(Session session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the tracked active session (<c>LogoutAt is null</c>) for the given identity
    /// and IP, or <c>null</c> if none is open. Returns the most recently opened one if
    /// duplicates exist.
    /// </summary>
    Task<Session?> GetActiveByCompoudId(
        string tenantId,
        string username,
        string ip,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A no-tracking <see cref="IQueryable{T}"/> over all sessions (active and historical)
    /// for arbitrary read queries.
    /// </summary>
    IQueryable<Session> Query();

    /// <summary>Flushes staged changes to the database and returns the affected row count.</summary>
    Task<int> SaveChanges(CancellationToken cancellationToken = default);
}
