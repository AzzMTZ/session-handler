using Microsoft.EntityFrameworkCore;
using SessionHandler.Models;

namespace SessionHandler.Data;

/// <summary>
/// EF Core context for session persistence. The connection string is supplied by DI
/// (<c>AddDbContext</c> + <c>UseSqlite</c> in <c>Program.cs</c>), not an <c>OnConfiguring</c> override.
/// </summary>
public class SessionDbContext : DbContext
{
    public SessionDbContext(DbContextOptions<SessionDbContext> options)
        : base(options)
    {
    }

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<SessionEvent> SessionEvents => Set<SessionEvent>();

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

        modelBuilder.Entity<SessionEvent>(sessionEvent =>
        {
            sessionEvent.HasKey(e => e.Id);

            sessionEvent.Property(e => e.TenantId).IsRequired();
            sessionEvent.Property(e => e.Username).IsRequired();
            sessionEvent.Property(e => e.Ip).IsRequired();

            sessionEvent.HasOne(e => e.Session)
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Backs "history for this exact session" — the common case.
            sessionEvent.HasIndex(e => e.SessionId);

            // Backs the same identity/IP lookups as Sessions, applied to events.
            sessionEvent.HasIndex(e => new { e.TenantId, e.Username, e.Ip });

            // Backs "all events of this type" (e.g. tag-change history via Type == Update).
            sessionEvent.HasIndex(e => e.Type);

            // Backs time-range filtering.
            sessionEvent.HasIndex(e => e.Timestamp);
        });
    }
}
