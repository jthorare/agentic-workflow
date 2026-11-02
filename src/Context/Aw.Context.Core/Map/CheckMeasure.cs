namespace Aw.Context.Core.Map;

/// <summary>One structure measure of a map: how many of its leaf home features trip it, and the two limits (map structure spec §4.2).</summary>
/// <param name="Name">'isolated' or 'over-cap'.</param>
/// <param name="Count">The features that trip it.</param>
/// <param name="Total">The features it was measured over (0 when it wasn't measured).</param>
/// <param name="DegradedPermille">The degraded limit in tenths of a percent (300 is 30%).</param>
/// <param name="RefusePermille">The refuse limit in tenths of a percent.</param>
/// <param name="Skipped">Why it wasn't measured, or null when it was.</param>
public sealed record CheckMeasure(string Name, int Count, int Total, int DegradedPermille, int RefusePermille, string? Skipped = null)
{
    /// <summary>How the measure came out: a measure that wasn't measured, or sits at or under its degraded limit, passes.</summary>
    public CheckVerdict Verdict =>
        Skipped is not null || Count * 1000L <= DegradedPermille * (long)Total ? CheckVerdict.Pass
        : Count * 1000L <= RefusePermille * (long)Total ? CheckVerdict.Degraded
        : CheckVerdict.Refuse;

    /// <summary>The share of the features that trip it, in tenths of a percent, rounded up (so a share shown next to its limit never reads as at the limit when it is over).</summary>
    public int Permille => Total == 0 ? 0 : (int)(((Count * 1000L) + Total - 1) / Total);
}
