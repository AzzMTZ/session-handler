using System.Net;
using SessionHandler.Dtos;
using SessionHandler.Models;
using SessionHandler.Tests.Infrastructure;

namespace SessionHandler.Tests;

/// <summary>
/// Concurrent requests for one identity triple must not race: the per-identity lock
/// plus the partial unique index should leave the data in exactly one valid state.
/// </summary>
public class SessionConcurrencyTests(SessionHandlerApp app) : ApiTestBase(app.CreateClient()), IClassFixture<SessionHandlerApp>
{
    private static readonly DateTime T0 = new(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Parallel_logins_for_the_same_triple_yield_exactly_one_session()
    {
        var tenant = NewId("tenant");
        var login = new LoginEvent(tenant, "alice", "10.0.0.1", [], T0);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => PostLogin(login)));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        Assert.Equal(1, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(19, statuses.Count(s => s == HttpStatusCode.Conflict));

        var active = await SearchSessions(new SessionQuery { TenantId = tenant });
        Assert.Single(active);

        var events = await SearchEvents(new SessionEventQuery { TenantId = tenant });
        Assert.Single(events);
        Assert.Equal(SessionEventType.Login, events[0].Type);
    }

    [Fact]
    public async Task Parallel_updates_on_one_session_all_apply_and_leave_the_latest_state()
    {
        var tenant = NewId("tenant");
        await LoginOk(new LoginEvent(tenant, "bob", "10.0.0.2", ["start"], T0));

        var updates = Enumerable.Range(1, 10).Select(i =>
            PutUpdate(tenant, "bob", "10.0.0.2",
                new UpdateSessionRequest([$"v{i}"], T0.AddMinutes(i))));
        var responses = await Task.WhenAll(updates);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var active = Assert.Single(await SearchSessions(new SessionQuery { TenantId = tenant }));
        Assert.Equal(T0.AddMinutes(10), active.LastSeenAt);
        Assert.Equal(["v10"], active.Tags);

        // One Update event per request, plus the original Login.
        var events = await SearchEvents(new SessionEventQuery { TenantId = tenant });
        Assert.Equal(10, events.Count(e => e.Type == SessionEventType.Update));
    }
}
