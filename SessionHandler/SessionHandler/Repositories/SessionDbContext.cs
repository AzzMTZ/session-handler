using Microsoft.EntityFrameworkCore;
using SessionHandler.Models;

namespace SessionHandler.Data;

/// <summary>
/// EF Core database context for session persistence, following the structure in
/// https://learn.microsoft.com/en-us/ef/core/get-started/overview/first-app
/// (entity type + <see cref="DbSet{TEntity}"/> + migrations). The only deviation
/// from that tutorial is the connection wiring: because this is an ASP.NET Core
/// app the connection string is supplied by dependency injection
/// (<c>AddDbContext</c> + <c>UseSqlite</c> in <c>Program.cs</c>) instead of an
/// <c>OnConfiguring</c> override.
/// </summary>
public class SessionDbContext : DbContext
{
    public SessionDbContext(DbContextOptions<SessionDbContext> options)
        : base(options)
    {
    }

    public DbSet<Session> Sessions => Set<Session>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Session>(session =>
        {
            session.HasKey(s => s.Id);

            session.Property(s => s.TenantId).IsRequired();
            session.Property(s => s.Username).IsRequired();
            session.Property(s => s.Ip).IsRequired();

            // Backs the common "sessions for this identity / this IP" lookups.
            session.HasIndex(s => new { s.TenantId, s.Username, s.Ip });

            // Backs "active sessions only" filtering used by most queries.
            session.HasIndex(s => s.LogoutAt);
        });
    }
}
