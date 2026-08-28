using Microsoft.EntityFrameworkCore;
using SessionHandler.Data;
using SessionHandler.Interfaces;
using SessionHandler.Models;

namespace SessionHandler.Repositories;

/// <inheritdoc cref="ISessionEventRepository" />
public class SessionEventRepository(SessionDbContext db) : ISessionEventRepository
{
    public async Task<SessionEvent> Add(SessionEvent sessionEvent, CancellationToken cancellationToken = default)
    {
        var result = await db.SessionEvents.AddAsync(sessionEvent, cancellationToken);
        return result.Entity;
    }

    public Task<SessionEvent?> GetById(int id, CancellationToken cancellationToken = default) =>
        db.SessionEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public IQueryable<SessionEvent> Query() => db.SessionEvents.AsNoTracking();

    public Task<int> SaveChanges(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
