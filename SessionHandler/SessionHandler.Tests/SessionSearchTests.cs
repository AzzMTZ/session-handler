using SessionHandler.Dtos;
using SessionHandler.Tests.Infrastructure;

namespace SessionHandler.Tests;

/// <summary>
/// <c>POST /sessions/search</c> against the <see cref="SeededSessionsFixture"/> dataset:
/// each attribute filter, the conjunctive tag filter, the active-only default, and the
/// date-range bounds with their ordering.
/// </summary>
public class SessionSearchTests(SeededSessionsFixture data)
    : ApiTestBase(data.Client), IClassFixture<SeededSessionsFixture>
{
    [Fact]
    public async Task Filters_by_tenant()
    {
        var acme = await SearchSessions(new SessionQuery { TenantId = data.Acme });
        var globex = await SearchSessions(new SessionQuery { TenantId = data.Globex });

        Assert.Equal(["carol", "alice"], acme.Select(s => s.Username)); // active only, LoginAt desc
        Assert.Equal(["dave"], globex.Select(s => s.Username));
    }

    [Fact]
    public async Task Filters_by_username_and_ip()
    {
        var byName = await SearchSessions(new SessionQuery { TenantId = data.Acme, Username = "alice" });
        Assert.Equal("1.1.1.1", Assert.Single(byName).Ip);

        var byIp = await SearchSessions(new SessionQuery { TenantId = data.Acme, Ip = "3.3.3.3" });
        Assert.Equal("carol", Assert.Single(byIp).Username);
    }

    [Fact]
    public async Task Tag_filter_requires_every_tag_to_match()
    {
        var admins = await SearchSessions(new SessionQuery { TenantId = data.Acme, Tags = ["role:admin"] });
        Assert.Equal(["carol", "alice"], admins.Select(s => s.Username));

        var blueAdmins = await SearchSessions(
            new SessionQuery { TenantId = data.Acme, Tags = ["role:admin", "team:blue"] });
        Assert.Equal("alice", Assert.Single(blueAdmins).Username);
    }

    [Fact]
    public async Task Active_only_is_the_default_and_can_be_opted_out_of()
    {
        var active = await SearchSessions(new SessionQuery { TenantId = data.Acme });
        Assert.DoesNotContain(active, s => s.Username == "bob");

        var all = await SearchSessions(new SessionQuery { TenantId = data.Acme, ActiveOnly = false });
        Assert.Contains(all, s => s.Username == "bob" && s.LogoutAt != null);
    }

    [Fact]
    public async Task Login_date_range_bounds_are_inclusive_and_results_are_newest_first()
    {
        var results = await SearchSessions(new SessionQuery
        {
            TenantId = data.Acme,
            ActiveOnly = false,
            LoginAt = new DateRange(data.T0.AddHours(1), data.T0.AddHours(2)),
        });

        // bob (T0+1h) and carol (T0+2h) sit exactly on the bounds; alice (T0) is out.
        Assert.Equal(["carol", "bob"], results.Select(s => s.Username));
    }
}
