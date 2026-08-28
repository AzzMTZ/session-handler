using Microsoft.EntityFrameworkCore;
using SessionHandler.Data;
using SessionHandler.Interfaces;
using SessionHandler.Models;

namespace SessionHandler.Repositories;

/// <inheritdoc cref="ISessionRepository" />
public class SessionRepository(SessionDbContext db) : ISessionRepository
{
    public async Task<Session> Add(Session session, CancellationToken cancellationToken = default)
    {
        var result = await db.Sessions.AddAsync(session, cancellationToken);
        return result.Entity;
    }

    public Task<Session?> GetActiveByCompoundId(
        string tenantId,
        string username,
        string ip,
        CancellationToken cancellationToken = default) =>
        db.Sessions
            .Where(s => s.LogoutAt == null
                        && s.TenantId == tenantId
                        && s.Username == username
                        && s.Ip == ip)
            .OrderByDescending(s => s.LoginAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Session?> GetById(int id, CancellationToken cancellationToken = default) =>
        db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public IQueryable<Session> Query() => db.Sessions.AsNoTracking();

    public Task<int> SaveChanges(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}