using Microsoft.EntityFrameworkCore;
using SessionHandler.Data;
using SessionHandler.Interfaces;
using SessionHandler.Models;

namespace SessionHandler.Repositories;

/// <inheritdoc cref="ISessionRepository" />
public class SessionRepository : ISessionRepository
{
    private readonly SessionDbContext _db;

    public SessionRepository(SessionDbContext db) => _db = db;

    public async Task<Session> AddAsync(Session session, CancellationToken cancellationToken = default)
    {
        await _db.Sessions.AddAsync(session, cancellationToken);
        return session;
    }

    public Task<Session?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Sessions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Session?> GetActiveAsync(
        string tenantId,
        string username,
        string ip,
        CancellationToken cancellationToken = default) =>
        _db.Sessions
            .Where(s => s.LogoutAt == null
                        && s.TenantId == tenantId
                        && s.Username == username
                        && s.Ip == ip)
            .OrderByDescending(s => s.LoginAt)
            .FirstOrDefaultAsync(cancellationToken);

    public IQueryable<Session> Query() => _db.Sessions.AsNoTracking();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
