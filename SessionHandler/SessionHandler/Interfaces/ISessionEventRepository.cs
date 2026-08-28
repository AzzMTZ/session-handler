using SessionHandler.Models;

namespace SessionHandler.Interfaces;

/// <summary>
/// Data access layer for <see cref="SessionEvent"/> rows. Events are append-only —
/// there is deliberately no Update/Delete here, mirroring <see cref="ISessionRepository"/>,
/// which has none either despite Session rows being mutable: EF Core's change tracker
/// handles in-place mutation, and nothing in the domain ever mutates or removes an
/// event once recorded. <see cref="SaveChanges"/> flushes the shared
/// <c>SessionDbContext</c>, so an event added here and a session change staged via
/// <see cref="ISessionRepository"/> in the same request commit as one transaction.
/// </summary>
public interface ISessionEventRepository
{
    /// <summary>Stages a new session event for insertion.</summary>
    Task<SessionEvent> Add(SessionEvent sessionEvent, CancellationToken cancellationToken = default);

    /// <summary>Loads a session event by id, or <c>null</c> if none exists.</summary>
    Task<SessionEvent?> GetById(int id, CancellationToken cancellationToken = default);

    /// <summary>A no-tracking <see cref="IQueryable{T}"/> over all session events for arbitrary read queries.</summary>
    IQueryable<SessionEvent> Query();

    /// <summary>Flushes staged changes to the database and returns the affected row count.</summary>
    Task<int> SaveChanges(CancellationToken cancellationToken = default);
}
