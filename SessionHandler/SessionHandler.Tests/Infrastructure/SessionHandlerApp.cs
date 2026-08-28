using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace SessionHandler.Tests.Infrastructure;

/// <summary>
/// Boots the real API in-process (all controllers, DI, EF Core, migrations, the
/// exception handler) with one thing swapped: the SQLite connection string points
/// at a private in-memory database instead of the runtime <c>sessions.db</c> file.
/// Each factory instance gets its own database, so test classes running in parallel
/// never see each other's rows.
/// </summary>
public sealed class SessionHandlerApp : WebApplicationFactory<Program>
{
    // A uniquely-named shared-cache in-memory database. `Cache=Shared` lets the
    // several scoped SessionDbContexts a burst of concurrent requests creates each
    // open their own connection to the same database; the keep-alive connection
    // below keeps that database alive for the lifetime of the factory (a shared
    // in-memory database vanishes the instant its last connection closes).
    private readonly string _connectionString =
        new SqliteConnectionStringBuilder
        {
            DataSource = $"sessionhandler-tests-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

    private SqliteConnection? _keepAlive;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive = new SqliteConnection(_connectionString);
        _keepAlive.Open();

        // Program.cs builds the DbContext from GetConnectionString("SessionDb");
        // overriding that configuration value is enough to redirect it, with no
        // need to tear the registration out of the service collection. The
        // schema is then created by the db.Database.Migrate() call Program.cs
        // already runs on startup.
        builder.UseSetting("ConnectionStrings:SessionDb", _connectionString);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive?.Dispose();
        }
    }
}
