namespace SessionHandler.Utils;

public static class DateTimeUtils
{
    /// <summary>
    /// Normalizes a <see cref="DateTime"/> to UTC: converts <see cref="DateTimeKind.Local"/>
    /// values, passes <see cref="DateTimeKind.Utc"/> ones through, and re-stamps
    /// <see cref="DateTimeKind.Unspecified"/> ones as UTC without converting — used both
    /// for inbound timestamps (assumed already UTC when no kind is given) and for values
    /// read back from SQLite, which round-trips <see cref="DateTime"/> as text with no kind
    /// at all even though the stored value is always UTC.
    /// </summary>
    public static DateTime AsUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
