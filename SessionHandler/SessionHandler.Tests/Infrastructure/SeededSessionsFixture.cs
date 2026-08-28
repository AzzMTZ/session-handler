using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SessionHandler.Dtos;

namespace SessionHandler.Tests.Infrastructure;

/// <summary>
/// A fixed set of sessions, created once through the real API, for the read-only
/// search tests to query. Two tenants, one closed session, overlapping tags and
/// login times so every filter has something to bite on.
/// </summary>
public sealed class SeededSessionsFixture : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly SessionHandlerApp _app = new();

    public HttpClient Client { get; private set; } = null!;

    public string Acme { get; } = $"acme-{Guid.NewGuid():N}";
    public string Globex { get; } = $"globex-{Guid.NewGuid():N}";
    public DateTime T0 { get; } = new(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        Client = _app.CreateClient();

        // Acme / alice — active, two tags.
        await Login(new LoginEvent(Acme, "alice", "1.1.1.1", ["role:admin", "team:blue"], T0));

        // Acme / bob — logged out at T0 + 3h.
        await Login(new LoginEvent(Acme, "bob", "2.2.2.2", ["team:blue"], T0.AddHours(1)));
        await Logout(Acme, "bob", "2.2.2.2", T0.AddHours(3));

        // Acme / carol — active, one shared tag.
        await Login(new LoginEvent(Acme, "carol", "3.3.3.3", ["role:admin"], T0.AddHours(2)));

        // Globex / dave — active, different tenant.
        await Login(new LoginEvent(Globex, "dave", "4.4.4.4", ["team:red"], T0.AddMinutes(30)));
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }

    private async Task Login(LoginEvent login)
    {
        var response = await Client.PostAsync("/sessions",
            JsonContent.Create(login, options: Json));
        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException(
                $"Seed login failed: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }
    }

    private async Task Logout(string tenantId, string username, string ip, DateTime at)
    {
        var response = await Client.SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, $"/sessions/{tenantId}/{username}/{ip}")
            {
                Content = JsonContent.Create(new LogoutSessionRequest(at), options: Json),
            });
        if (response.StatusCode != HttpStatusCode.NoContent)
        {
            throw new InvalidOperationException(
                $"Seed logout failed: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }
    }
}
