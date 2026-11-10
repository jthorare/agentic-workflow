using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>The Work page's states (<see cref="QueueViewModel"/>, continued): the nothing box, no match, the rebuild line in the groups and the footer.</summary>
internal sealed partial class QueueViewModel
{
    /// <summary>True when the queue holds nothing at all and no filter is on.</summary>
    public bool ShowsNothing { get; private set => Set(ref field, value); }

    /// <summary>True when a filter is on and no row matches it.</summary>
    public bool ShowsNoMatch { get; private set => Set(ref field, value); }

    /// <summary>The one line the empty queue shows.</summary>
#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    public string NothingText => string.Empty;

    /// <summary>The old box title (red stub; the view still binds it until the implementation).</summary>
    public string NothingTitle => Labels.NothingInQueue;

    /// <summary>The link under it that opens the Create item form.</summary>
    public string CreateLinkText => string.Empty;

    /// <summary>The no-match box's title.</summary>
    public string NoMatchTitle => Labels.NoItemsMatch;

    /// <summary>The no-match box's button.</summary>
    public string ClearFilterText => Labels.ClearFilter;
#pragma warning restore CA1822, MA0041

    /// <summary>"Up to date with the ledger (event 214)." or "Updated up to event 8 of 9."; empty before a read.</summary>
    public string FooterText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the table has rows: the column headers show only then.</summary>
    public bool ShowsColumns { get; private set => Set(ref field, value); }

    /// <summary>True when some shown row has a repo: the Repo column shows only then.</summary>
    public bool ShowsRepoColumn { get; private set => Set(ref field, value); }

    /// <summary>True when at least one closed item exists: the Closed fold shows only then.</summary>
    public bool ShowsClosed { get; private set => Set(ref field, value); }

    /// <summary>True when the footer has text.</summary>
    public bool HasFooter => FooterText.Length > 0;

    private void States(QueueView view)
    {
        var unmatched = (FilterText.Length > 0 || SelectedRepo.Length > 0) && !Visible().Any();
        ShowsNoMatch = unmatched;
        ShowsNothing = IsEmpty && !unmatched;
        FooterText = view.AsOf.Cursor >= view.AsOf.LedgerHead ? Labels.QueueUpToDate(view.AsOf.LedgerHead) : Labels.QueueBehind(view.AsOf.Cursor, view.AsOf.LedgerHead);
    }

    // While the views rebuild every group but the queued one says so instead of its own sentence. Stalled wins over Rebuilding (a rebuild that
    // stalls again carries both): the banner says it once, and the panels keep their stale cue instead of repeating the sentence.
    private string? EmptyLine(QueueGroupView group) => _view?.AsOf switch
    {
        { Stalled: true } => string.Empty,
        { Rebuilding: true } asOf when !string.Equals(group.Group, Labels.QueuedGroup, StringComparison.Ordinal) => Labels.RebuildingViews(asOf.Cursor, asOf.LedgerHead),
        _ => group.Empty,
    };
}
