namespace SessionHandler.Dtos;

/// <summary>
/// A lower and upper bound on a timestamp. Both ends are optional: supply only
/// <see cref="Since"/> for "at or after", only <see cref="Until"/> for "up to", or
/// both for a window.
/// </summary>
public record DateRange(DateTime? Since, DateTime? Until);
