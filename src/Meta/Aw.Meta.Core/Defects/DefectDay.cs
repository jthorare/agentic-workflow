using System.Globalization;

namespace Aw.Meta.Core.Defects;

/// <summary>The stored per-day counts of `report-defect`: "yyyy-MM-dd|filed|skipped".</summary>
public static class DefectDay
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>Reads the stored value.</summary>
    /// <param name="stored">The value under <see cref="KvKeys.DefectsDay"/>, or null.</param>
    /// <param name="today">The UTC date now.</param>
    /// <returns>The counts for today; 0 and 0 when the value is absent, unreadable or of another day.</returns>
    public static (int Filed, int Skipped) Parse(string? stored, DateOnly today)
    {
        var parts = stored?.Split('|');
        return parts is [var date, var filed, var skipped]
            && date == today.ToString(DateFormat, CultureInfo.InvariantCulture)
            && int.TryParse(filed, NumberStyles.None, CultureInfo.InvariantCulture, out var f)
            && int.TryParse(skipped, NumberStyles.None, CultureInfo.InvariantCulture, out var s)
            ? (f, s)
            : (0, 0);
    }

    /// <summary>Writes the counts.</summary>
    /// <param name="today">The UTC date now.</param>
    /// <param name="filed">Issues filed today.</param>
    /// <param name="skipped">Defects skipped today because the cap was hit.</param>
    /// <returns>The value to store.</returns>
    public static string Format(DateOnly today, int filed, int skipped) =>
        string.Create(CultureInfo.InvariantCulture, $"{today.ToString(DateFormat, CultureInfo.InvariantCulture)}|{filed}|{skipped}");
}
