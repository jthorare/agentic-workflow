namespace Aw.Sindri.Core.NeedsYou;

/// <summary>The order of the human's list: urgency (pulse, colour, calm), then since, then id.</summary>
public static class NeedsYouOrder
{
    /// <summary>Compares two rows.</summary>
    /// <param name="left">A row.</param>
    /// <param name="right">Another row.</param>
    /// <returns>Negative when <paramref name="left"/> comes first.</returns>
    public static int Compare(NeedsYouRow left, NeedsYouRow right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var byUrgency = Rank(left.Urgency).CompareTo(Rank(right.Urgency));
        if (byUrgency != 0)
        {
            return byUrgency;
        }

        var bySince = left.Since.CompareTo(right.Since);
        return bySince != 0 ? bySince : string.CompareOrdinal(left.Id.Value, right.Id.Value);
    }

    /// <summary>The rows in order.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The sorted rows.</returns>
    public static IReadOnlyList<NeedsYouRow> Sort(IEnumerable<NeedsYouRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var sorted = rows.ToList();
        sorted.Sort(Compare);
        return sorted;
    }

    private static int Rank(Urgency urgency) => urgency switch
    {
        Urgency.Pulse => 0,
        Urgency.Colour => 1,
        Urgency.Calm => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(urgency), urgency, "not an urgency"),
    };
}
