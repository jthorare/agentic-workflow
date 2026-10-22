namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page's client filters (F3 workflows spec §4 "Needs you filter, Mine", "Search"): pure, over the rows loaded.</summary>
internal static class WorkflowFilters
{
    /// <summary>Whether a run passes the filter and the search.</summary>
    /// <param name="row">The run.</param>
    /// <param name="filter">The segment chosen.</param>
    /// <param name="search">The search text; blank for none. Matches the title, the current step and the reference.</param>
    /// <returns>True when the run shows.</returns>
    internal static bool Matches(InstanceRowViewModel row, WorkflowFilter filter, string? search) =>
        InFilter(row, filter) && MatchesSearch(row, search);

    /// <summary>Whether a run passes a filter segment alone.</summary>
    /// <param name="row">The run.</param>
    /// <param name="filter">The segment.</param>
    /// <returns>True when the run is in the segment.</returns>
    internal static bool InFilter(InstanceRowViewModel row, WorkflowFilter filter) => filter switch
    {
        WorkflowFilter.NeedsYou => NeedsYou(row),
        WorkflowFilter.Mine => NeedsYou(row) || row.Status.Kind == RunStatusKind.TookOver,
        WorkflowFilter.All => true,
        _ => true,
    };

    /// <summary>The counts the segments show, over the rows loaded.</summary>
    /// <param name="rows">The rows loaded.</param>
    /// <returns>The count of each segment.</returns>
    internal static (int All, int NeedsYou, int Mine) Counts(IReadOnlyCollection<InstanceRowViewModel> rows) =>
        (rows.Count, rows.Count(r => InFilter(r, WorkflowFilter.NeedsYou)), rows.Count(r => InFilter(r, WorkflowFilter.Mine)));

    private static bool NeedsYou(InstanceRowViewModel row) => row.Status.Kind is RunStatusKind.WaitingForYou or RunStatusKind.Paused;

    private static bool MatchesSearch(InstanceRowViewModel row, string? search) =>
        string.IsNullOrWhiteSpace(search)
        || new[] { row.Subject, row.Now, row.Address }.Any(text => text.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
}
