using System.Net;
using SessionHandler.Dtos;
using SessionHandler.Models;
using SessionHandler.Tests.Infrastructure;

namespace SessionHandler.Tests;

/// <summary>
/// The event log is written as a side effect of the session endpoints. It must record
/// exactly one event per <em>successful</em> Login/Update/Logout and nothing for a
/// rejected call — the session change and its event commit as one transaction.
/// </summary>
public class SessionEventInvariantTests(SessionHandlerApp app)
    : ApiTestBase(app.CreateClient()), IClassFixture<SessionHandlerApp>
{
    private static readonly DateTime T0 = new(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Login_update_logout_each_append_one_matching_event()
    {
        var tenant = NewId("tenant");
        var session = await LoginOk(new LoginEvent(tenant, "alice", "10.0.0.1", ["team:blue"], T0));

        await PutUpdate(tenant, "alice", "10.0.0.1",
            new UpdateSessionRequest(["team:green"], T0.AddMinutes(10)));
        await DeleteLogout(tenant, "alice", "10.0.0.1", new LogoutSessionRequest(T0.AddMinutes(20)));

        var events = await SearchEvents(new SessionEventQuery { SessionId = session.Id });

        // Newest first.
        Assert.Equal(
            [SessionEventType.Logout, SessionEventType.Update, SessionEventType.Login],
            events.Select(e => e.Type));
        Assert.All(events, e => Assert.Equal(session.Id, e.SessionId));

        var login = events.Single(e => e.Type == SessionEventType.Login);
        Assert.Equal(["team:blue"], login.Tags);

        var logout = events.Single(e => e.Type == SessionEventType.Logout);
        Assert.Null(logout.Tags);
    }

    [Fact]
    public async Task A_rejected_login_conflict_adds_no_second_event()
    {
        var tenant = NewId("tenant");
        var login = new LoginEvent(tenant, "bob", "10.0.0.2", [], T0);
        await LoginOk(login);

        var conflict = await PostLogin(login with { Timestamp = T0.AddMinutes(1) });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var events = await SearchEvents(new SessionEventQuery { TenantId = tenant });
        Assert.Equal(SessionEventType.Login, Assert.Single(events).Type);
    }

    [Fact]
    public async Task Update_and_logout_that_404_add_no_events()
    {
        var tenant = NewId("tenant");

        await PutUpdate(tenant, "carol", "10.0.0.3", new UpdateSessionRequest([], T0));
        await DeleteLogout(tenant, "carol", "10.0.0.3", new LogoutSessionRequest(T0));

        var events = await SearchEvents(new SessionEventQuery { TenantId = tenant });
        Assert.Empty(events);
    }

    [Fact]
    public async Task Out_of_order_update_is_recorded_but_does_not_move_session_state_backwards()
    {
        var tenant = NewId("tenant");
        var session = await LoginOk(new LoginEvent(tenant, "dave", "10.0.0.4", ["current"], T0.AddHours(1)));

        // Timestamp earlier than LastSeenAt: the event is still logged, but the
        // session's tags and LastSeenAt should not regress.
        var response = await PutUpdate(tenant, "dave", "10.0.0.4",
            new UpdateSessionRequest(["stale"], T0));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var current = await ReadAs<SessionResponse>(await GetSession(session.Id));
        Assert.Equal(["current"], current.Tags);
        Assert.Equal(T0.AddHours(1), current.LastSeenAt);

        var events = await SearchEvents(new SessionEventQuery { SessionId = session.Id });
        Assert.Contains(events, e => e.Type == SessionEventType.Update && e.Timestamp == T0);
    }
}
