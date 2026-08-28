using System.Net;
using SessionHandler.Dtos;
using SessionHandler.Tests.Infrastructure;

namespace SessionHandler.Tests;

/// <summary>
/// The session lifecycle over HTTP — create, read, update, close — plus the error
/// responses each endpoint declares (400 / 404 / 409).
/// </summary>
public class SessionCrudTests(SessionHandlerApp app) : ApiTestBase(app.CreateClient()), IClassFixture<SessionHandlerApp>
{
    private static readonly DateTime T0 = new(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Login_creates_an_active_session_and_returns_its_location()
    {
        var tenant = NewId("tenant");
        var login = new LoginEvent(tenant, "alice", "10.0.0.1", ["team:blue"], T0);

        var response = await PostLogin(login);
        var session = await ReadAs<SessionResponse>(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"/sessions/{session.Id}", response.Headers.Location?.AbsolutePath);
        Assert.True(session.Id > 0);
        Assert.Equal(tenant, session.TenantId);
        Assert.Equal("alice", session.Username);
        Assert.Equal("10.0.0.1", session.Ip);
        Assert.Equal(["team:blue"], session.Tags);
        Assert.Equal(T0, session.LoginAt);
        Assert.Equal(T0, session.LastSeenAt);
        Assert.Null(session.LogoutAt);
    }

    [Fact]
    public async Task Get_returns_the_session_by_id_and_404_when_it_does_not_exist()
    {
        var created = await LoginOk(new LoginEvent(NewId("tenant"), "bob", "10.0.0.2", [], T0));

        var found = await GetSession(created.Id);
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(created.Id, (await ReadAs<SessionResponse>(found)).Id);

        var missing = await GetSession(999_999);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Update_replaces_tags_and_advances_last_seen_on_the_active_session()
    {
        var tenant = NewId("tenant");
        await LoginOk(new LoginEvent(tenant, "carol", "10.0.0.3", ["old"], T0));

        var response = await PutUpdate(tenant, "carol", "10.0.0.3",
            new UpdateSessionRequest(["new1", "new2"], T0.AddMinutes(30)));
        var updated = await ReadAs<SessionResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["new1", "new2"], updated.Tags);
        Assert.Equal(T0.AddMinutes(30), updated.LastSeenAt);
        Assert.Equal(T0, updated.LoginAt);
        Assert.Null(updated.LogoutAt);
    }

    [Fact]
    public async Task Logout_closes_the_active_session()
    {
        var tenant = NewId("tenant");
        var created = await LoginOk(new LoginEvent(tenant, "dave", "10.0.0.4", [], T0));

        var response = await DeleteLogout(tenant, "dave", "10.0.0.4",
            new LogoutSessionRequest(T0.AddHours(1)));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = await ReadAs<SessionResponse>(await GetSession(created.Id));
        Assert.Equal(T0.AddHours(1), after.LogoutAt);
    }

    [Fact]
    public async Task Login_for_an_already_active_triple_is_a_conflict()
    {
        var tenant = NewId("tenant");
        var login = new LoginEvent(tenant, "erin", "10.0.0.5", [], T0);
        await LoginOk(login);

        var second = await PostLogin(login with { Timestamp = T0.AddMinutes(1) });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Update_and_logout_without_an_active_session_are_not_found()
    {
        var tenant = NewId("tenant");

        var update = await PutUpdate(tenant, "frank", "10.0.0.6",
            new UpdateSessionRequest([], T0));
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        var logout = await DeleteLogout(tenant, "frank", "10.0.0.6",
            new LogoutSessionRequest(T0));
        Assert.Equal(HttpStatusCode.NotFound, logout.StatusCode);
    }

    [Fact]
    public async Task Logging_in_again_after_logout_opens_a_new_distinct_session()
    {
        var tenant = NewId("tenant");
        var login = new LoginEvent(tenant, "grace", "10.0.0.7", [], T0);

        var first = await LoginOk(login);
        await DeleteLogout(tenant, "grace", "10.0.0.7", new LogoutSessionRequest(T0.AddHours(1)));
        var second = await LoginOk(login with { Timestamp = T0.AddHours(2) });

        Assert.NotEqual(first.Id, second.Id);
        Assert.Null(second.LogoutAt);

        // Both rows are retained: an ActiveOnly:false search sees the closed one too.
        var all = await SearchSessions(new SessionQuery { TenantId = tenant, ActiveOnly = false });
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Login_with_a_malformed_body_is_a_400_validation_problem()
    {
        // Tags / TenantId / Username / Ip are non-nullable reference types, so the
        // [ApiController] model binder rejects a body that omits them.
        var response = await Client.PostAsync("/sessions", Body(new { }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Timestamps_are_normalised_to_utc_in_responses()
    {
        var tenant = NewId("tenant");
        // A local-kind instant serialises with this machine's offset; the API is
        // expected to convert it to UTC on the way back out.
        var localLogin = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Local);

        var session = await LoginOk(new LoginEvent(tenant, "heidi", "10.0.0.8", [], localLogin));

        Assert.Equal(DateTimeKind.Utc, session.LoginAt.Kind);
        Assert.Equal(localLogin.ToUniversalTime(), session.LoginAt);
    }
}
