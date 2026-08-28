namespace SessionHandler.Interfaces;

/// <summary>
/// The single commit point for changes staged across <see cref="ISessionRepository"/>
/// and <see cref="ISessionEventRepository"/> on the shared <c>SessionDbContext</c>:
/// neither exposes its own <c>SaveChanges</c>, so a session and its event always
/// commit as one transaction.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Flushes staged changes to the database and returns the affected row count.</summary>
    Task<int> SaveChanges(CancellationToken cancellationToken = default);
}
