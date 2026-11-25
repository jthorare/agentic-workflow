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
    public string NothingText => Labels.NothingInQueue;

    /// <summary>The link under it that opens the Create item form.</summary>
    public string CreateLinkText => Labels.Action("CreateItem");

    /// <summary>The no-match box's title.</summary>
    public string NoMatchTitle => Labels.NoItemsMatch;

    /// <summary>The no-match box's button.</summary>
    public string ClearFilterText => Labels.ClearFilter;
#pragma warning restore CA1822, MA0041

    /// <summary>Empty while the views are current (and before a read); "Updated 22:57, 2 min ago" while they are behind.</summary>
    public string FooterText { get; private set { Set(ref field, value); OnChanged(nameof(HasFooter)); } } = string.Empty;

    /// <summary>True when the table has rows: the column headers show only then.</summary>
    public bool ShowsColumns { get; private set => Set(ref field, value); }

    /// <summary>True when some shown row has a repo: the Repo column shows only then.</summary>
    public bool ShowsRepoColumn { get; private set => Set(ref field, value); }

    /// <summary>True when some shown row has an action: the action column shows only then.</summary>
    public bool ShowsActionColumn { get; private set => Set(ref field, value); }

    /// <summary>True when at least one closed item exists: the Closed fold shows only then.</summary>
    public bool ShowsClosed { get; private set => Set(ref field, value); }

    /// <summary>True when the footer has text.</summary>
    public bool HasFooter => FooterText.Length > 0;

    private void States(QueueView view)
    {
        var unmatched = (FilterText.Length > 0 || SelectedRepo.Length > 0) && !Visible().Any();
        ShowsNoMatch = unmatched;
        ShowsNothing = IsEmpty && !unmatched;
        FooterText = Footer(view.AsOf);
        FooterTicking(FooterText.Length > 0);
        Columns();
    }

    // "2 min ago" ages while the views stay behind and no refresh comes: a timer on the clock re-words it, only while the footer shows.
    private static readonly TimeSpan _footerTick = TimeSpan.FromSeconds(10);
    private readonly UiContext _ui = new();
    private ITimer? _footerTicker;

    private void FooterTicking(bool on)
    {
        if (on)
        {
            _footerTicker ??= clock.CreateTimer(_ => _ui.PostOrInline(RewordFooter), state: null, _footerTick, _footerTick);
            return;
        }

        _footerTicker?.Dispose();
        _footerTicker = null;
    }

    private void RewordFooter()
    {
        if (_view is { } view)
        {
            FooterText = Footer(view.AsOf);
        }
    }

    private string Footer(AsOfView asOf)
    {
        if (asOf.Cursor >= asOf.LedgerHead || asOf.ProjectedAt is not { } at)
        {
            return string.Empty;
        }

        return Labels.QueueUpdated(TimeZoneInfo.ConvertTime(at, clock.LocalTimeZone), clock.GetUtcNow() - at);
    }

    // Headers only over rows; the Repo column only while a shown row has a repo (the closed rows count while the fold is open).
    private void Columns()
    {
        var shown = Visible().ToList();
        ShowsColumns = shown.Count > 0 || (IsClosedOpen && ClosedRows.Count > 0);
        ShowsRepoColumn = shown.Concat(IsClosedOpen ? ClosedRows : []).Any(r => r.Repo.Length > 0);
        ShowsActionColumn = shown.Exists(r => r.HasPrimaryAction);
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
