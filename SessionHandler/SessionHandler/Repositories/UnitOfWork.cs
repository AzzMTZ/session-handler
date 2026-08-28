using SessionHandler.Data;
using SessionHandler.Interfaces;

namespace SessionHandler.Repositories;

/// <inheritdoc cref="IUnitOfWork" />
public class UnitOfWork(SessionDbContext db) : IUnitOfWork
{
    public Task<int> SaveChanges(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
