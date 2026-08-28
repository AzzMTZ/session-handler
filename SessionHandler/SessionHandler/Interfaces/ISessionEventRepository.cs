using SessionHandler.Models;

namespace SessionHandler.Interfaces;

/// <summary>
/// Data access layer for <see cref="SessionEvent"/> rows. Append-only: no Update/Delete,
/// since events are never changed once recorded. Commit staged changes via
/// <see cref="IUnitOfWork"/>, which flushes the same <c>SessionDbContext</c> as
/// <see cref="ISessionRepository"/> so both commit in one transaction.
/// </summary>
public interface ISessionEventRepository
{
    /// <summary>Stages a new session event for insertion.</summary>
    Task<SessionEvent> Add(SessionEvent sessionEvent, CancellationToken cancellationToken = default);

    /// <summary>Loads a session event by id, or <c>null</c> if none exists.</summary>
    Task<SessionEvent?> GetById(int id, CancellationToken cancellationToken = default);

    /// <summary>A no-tracking <see cref="IQueryable{T}"/> over all session events for arbitrary read queries.</summary>
    IQueryable<SessionEvent> Query();
}
