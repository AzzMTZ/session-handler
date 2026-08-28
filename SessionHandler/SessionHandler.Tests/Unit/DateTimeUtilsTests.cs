using SessionHandler.Utils;

namespace SessionHandler.Tests.Unit;

/// <summary>
/// <see cref="DateTimeUtils.AsUtc"/> has one branch per <see cref="DateTimeKind"/>:
/// pass UTC through, convert Local, and re-stamp Unspecified as UTC <em>without</em>
/// shifting it (SQLite reads values back with no kind even though they are stored UTC).
/// </summary>
public class DateTimeUtilsTests
{
    private static readonly DateTime WallClock = new(2026, 6, 1, 8, 30, 0);

    [Fact]
    public void Utc_values_pass_through_unchanged()
    {
        var utc = DateTime.SpecifyKind(WallClock, DateTimeKind.Utc);

        var result = utc.AsUtc();

        Assert.Equal(utc, result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void Unspecified_values_are_restamped_as_utc_without_being_shifted()
    {
        var unspecified = DateTime.SpecifyKind(WallClock, DateTimeKind.Unspecified);

        var result = unspecified.AsUtc();

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(unspecified.Ticks, result.Ticks);
    }

    [Fact]
    public void Local_values_are_converted_to_the_equivalent_utc_instant()
    {
        var local = DateTime.SpecifyKind(WallClock, DateTimeKind.Local);

        var result = local.AsUtc();

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(local.ToUniversalTime(), result);
    }

    [Fact]
    public void AsUtc_is_idempotent()
    {
        var unspecified = DateTime.SpecifyKind(WallClock, DateTimeKind.Unspecified);

        var once = unspecified.AsUtc();
        var twice = once.AsUtc();

        Assert.Equal(once, twice);
        Assert.Equal(DateTimeKind.Utc, twice.Kind);
    }
}
