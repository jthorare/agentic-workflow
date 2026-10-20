namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>Which group of the inbox a row belongs to, by its id prefix and kind (needs-you spec §4).</summary>
internal static class NeedsYouGrouping
{
    /// <summary>The group of a row.</summary>
    /// <param name="entry">The row.</param>
    /// <returns>Its kind.</returns>
    internal static NeedsYouGroupKind KindOf(NeedsYouEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.DecisionId is not null || HasPrefix(entry, "decision:"))
        {
            return NeedsYouGroupKind.Decisions;
        }

        if (HasPrefix(entry, "desk:"))
        {
            return NeedsYouGroupKind.Approvals;
        }

        if (HasPrefix(entry, "park:") || HasPrefix(entry, "takeover:"))
        {
            return NeedsYouGroupKind.Runs;
        }

        if (HasPrefix(entry, "profile:") || HasPrefix(entry, "setup:"))
        {
            return NeedsYouGroupKind.Settings;
        }

        return HasPrefix(entry, "failure:") || string.Equals(entry.Kind, NeedsYouKinds.Failure, StringComparison.Ordinal) ? NeedsYouGroupKind.Failures : NeedsYouGroupKind.Other;
    }

    /// <summary>Splits rows into the inbox's groups: the canonical order, empty groups left out, each group's rows in the order given. Rows of a kind the app does not know form one group per served kind, after the known ones.</summary>
    /// <param name="rows">The rows, in the API's order.</param>
    /// <param name="entry">The served entry of a row.</param>
    /// <typeparam name="T">What a row is.</typeparam>
    /// <returns>The groups.</returns>
    internal static IReadOnlyList<NeedsYouGroup<T>> Split<T>(IEnumerable<T> rows, Func<T, NeedsYouEntry> entry)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(entry);
        return
        [
            .. rows
                .Select(row => (Row: row, Kind: KindOf(entry(row)), Heading: HeadingOf(entry(row))))
                .GroupBy(r => (r.Kind, r.Heading))
                .OrderBy(g => (int)g.Key.Kind)
                .Select(g => new NeedsYouGroup<T>(g.Key.Kind, g.Key.Heading, [.. g.Select(r => r.Row)])),
        ];
    }

    private static string HeadingOf(NeedsYouEntry entry) => KindOf(entry) switch
    {
        NeedsYouGroupKind.Approvals => Labels.NeedsYouGroupApprovals,
        NeedsYouGroupKind.Runs => Labels.NeedsYouGroupRuns,
        NeedsYouGroupKind.Decisions => Labels.NeedsYouGroupDecisions,
        NeedsYouGroupKind.Settings => Labels.NeedsYouGroupSettings,
        NeedsYouGroupKind.Failures => Labels.NeedsYouGroupFailures,
        NeedsYouGroupKind.Other => entry.Kind.Length > 0 ? entry.Kind : Labels.NeedsYouGroupOther,
        _ => throw new InvalidOperationException("a group the app doesn't know"),
    };

    private static bool HasPrefix(NeedsYouEntry entry, string prefix) => entry.Id.StartsWith(prefix, StringComparison.Ordinal);
}
