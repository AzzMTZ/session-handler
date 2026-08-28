namespace SessionHandler.Utils;

public static class DateTimeUtils
{
    /// <summary>
    /// Normalizes a <see cref="DateTime"/> to UTC. <see cref="DateTimeKind.Unspecified"/>
    /// values are stamped, not converted: both inbound timestamps and values read back
    /// from SQLite (which drops the kind) are already UTC.
    /// </summary>
    public static DateTime AsUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
