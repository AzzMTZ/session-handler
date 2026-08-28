namespace SessionHandler.Interfaces;

/// <summary>
/// Commits changes staged across repositories that share the same persistence
/// context. <see cref="ISessionRepository"/> and <see cref="ISessionEventRepository"/>
/// each stage inserts/mutations on the shared <c>SessionDbContext</c> but no longer
/// expose their own <c>SaveChanges</c> — a session and its event must always commit
/// together as one transaction, so there is exactly one commit point for both,
/// injected into the service that orchestrates them.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Flushes staged changes to the database and returns the affected row count.</summary>
    Task<int> SaveChanges(CancellationToken cancellationToken = default);
}
