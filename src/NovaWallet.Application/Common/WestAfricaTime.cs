namespace NovaWallet.Application.Common;

/// <summary>
/// West Africa Time conversions for the daily transfer limit.
/// </summary>
/// <remarks>
/// WAT is a fixed UTC+1 offset with no daylight saving, so this is a plain
/// arithmetic shift rather than a time-zone database lookup. That matters:
/// TimeZoneInfo.FindSystemTimeZoneById takes different ids on Windows
/// ("W. Central Africa Standard Time") and Linux ("Africa/Lagos"), and the
/// service is developed on one and runs in a container on the other.
/// Hardcoding the offset sidesteps the whole problem.
/// </remarks>
public static class WestAfricaTime
{
    public static readonly TimeSpan UtcOffset = TimeSpan.FromHours(1);

    /// <summary>The WAT calendar date that a given UTC instant falls within.</summary>
    public static DateOnly DateAt(DateTime utcNow) =>
        DateOnly.FromDateTime(utcNow.Add(UtcOffset));
}