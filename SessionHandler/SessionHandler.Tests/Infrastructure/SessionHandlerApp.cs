using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace SessionHandler.Tests.Infrastructure;

/// <summary>
/// Boots the real API in-process with one change: the SQLite connection string points
/// at a private in-memory database, unique per factory instance, so parallel test
/// classes never see each other's rows.
/// </summary>
public sealed class SessionHandlerApp : WebApplicationFactory<Program>
{
    // Shared-cache so the many scoped DbContexts a request burst creates all reach
    // the same database; the keep-alive connection below stops it vanishing when the
    // last request connection closes.
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

        // Overriding this config value redirects the DbContext; no need to touch the
        // service registration. Program.cs's startup Database.Migrate() builds the schema.
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
