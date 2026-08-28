using SessionHandler.Dtos;
using SessionHandler.Models;
using SessionHandler.Tests.Infrastructure;

namespace SessionHandler.Tests;

/// <summary>
/// <c>POST /session-events/search</c>: scoping to one session, filtering by type and
/// identity triple, and the timestamp range with its newest-first ordering. Each test
/// seeds its own flow through the real endpoints under a unique tenant.
/// </summary>
public class SessionEventSearchTests(SessionHandlerApp app)
    : ApiTestBase(app.CreateClient()), IClassFixture<SessionHandlerApp>
{
    private static readonly DateTime T0 = new(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Scopes_to_one_session_by_id_and_filters_by_type()
    {
        var tenant = NewId("tenant");
        var one = await LoginOk(new LoginEvent(tenant, "alice", "10.0.0.1", ["a"], T0));
        await PutUpdate(tenant, "alice", "10.0.0.1", new UpdateSessionRequest(["b"], T0.AddMinutes(5)));
        var two = await LoginOk(new LoginEvent(tenant, "bob", "10.0.0.2", ["a"], T0));
        await PutUpdate(tenant, "bob", "10.0.0.2", new UpdateSessionRequest(["b"], T0.AddMinutes(5)));

        var forOne = await SearchEvents(new SessionEventQuery { SessionId = one.Id });
        Assert.Equal(2, forOne.Count);
        Assert.All(forOne, e => Assert.Equal(one.Id, e.SessionId));

        var updates = await SearchEvents(new SessionEventQuery { TenantId = tenant, Type = SessionEventType.Update });
        Assert.Equal(2, updates.Count);
        Assert.Contains(updates, e => e.SessionId == one.Id);
        Assert.Contains(updates, e => e.SessionId == two.Id);
    }

    [Fact]
    public async Task Filters_by_identity_triple()
    {
        var tenant = NewId("tenant");
        await LoginOk(new LoginEvent(tenant, "alice", "10.0.0.1", [], T0));
        await LoginOk(new LoginEvent(tenant, "bob", "10.0.0.2", [], T0));

        var forAlice = await SearchEvents(new SessionEventQuery
        {
            TenantId = tenant,
            Username = "alice",
            Ip = "10.0.0.1",
        });

        Assert.Equal("alice", Assert.Single(forAlice).Username);
    }

    [Fact]
    public async Task Filters_by_timestamp_range_newest_first()
    {
        var tenant = NewId("tenant");
        var session = await LoginOk(new LoginEvent(tenant, "carol", "10.0.0.3", ["v0"], T0));
        await PutUpdate(tenant, "carol", "10.0.0.3", new UpdateSessionRequest(["v1"], T0.AddHours(1)));
        await PutUpdate(tenant, "carol", "10.0.0.3", new UpdateSessionRequest(["v2"], T0.AddHours(2)));
        await DeleteLogout(tenant, "carol", "10.0.0.3", new LogoutSessionRequest(T0.AddHours(3)));

        var windowed = await SearchEvents(new SessionEventQuery
        {
            SessionId = session.Id,
            Timestamp = new DateRange(T0.AddHours(1), T0.AddHours(2)),
        });

        Assert.Equal([T0.AddHours(2), T0.AddHours(1)], windowed.Select(e => e.Timestamp));
    }
}
