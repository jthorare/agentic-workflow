using System.Collections.ObjectModel;
using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The Workflows page's board, view switch, filter segments and search (<see cref="WorkflowsViewModel"/>, continued; F3 workflows spec §2, §3).
/// The board shows the line workflow's runs in six fixed lanes, and every other open run in a lane of its own before Done; the filters and the search narrow what both views draw, over the rows
/// already loaded, so changing one never re-reads the list.
/// </summary>
internal sealed partial class WorkflowsViewModel
{
    private string? _search;
    private int _onBoard;
    private readonly BoardLaneViewModel _otherLane = new(LaneId.Other);

    /// <summary>The six lanes of the board, in order; they stay even when empty.</summary>
    public ObservableCollection<BoardLaneViewModel> Lanes { get; } = [.. LineStages.Lanes.Select(l => new BoardLaneViewModel(l))];

    /// <summary>Raised when the page should put focus on the search box (the "/" key).</summary>
    internal event EventHandler? SearchFocusRequested;

    /// <summary>How the runs are drawn; the board until the human chooses the list.</summary>
    public WorkflowViewMode ViewMode { get; private set => Set(ref field, value); } = WorkflowViewMode.Board;

    /// <summary>True while the board shows.</summary>
    public bool IsBoard => ViewMode == WorkflowViewMode.Board;

    /// <summary>True while the list shows.</summary>
    public bool IsList => ViewMode == WorkflowViewMode.List;

    /// <summary>The view switch's selection: 0 the board, 1 the list.</summary>
    public int ViewIndex
    {
        get => (int)ViewMode;
        set => SetViewMode(value == (int)WorkflowViewMode.List ? WorkflowViewMode.List : WorkflowViewMode.Board);
    }

    /// <summary>The filter segment chosen.</summary>
    public WorkflowFilter Filter { get; private set => Set(ref field, value); }

    /// <summary>The filter segments' selection: 0 All, 1 Needs you, 2 Mine.</summary>
    public int FilterIndex
    {
        get => (int)Filter;
        set => SetFilter((WorkflowFilter)Math.Clamp(value, 0, 2));
    }

    /// <summary>The words the view switch and the filter segments read.</summary>
#pragma warning disable CA1822, MA0041 // fixed words are properties because the views bind them
    public string ViewBoardText => Labels.WorkflowsViewBoard;

    /// <summary>The list segment's words.</summary>
    public string ViewListText => Labels.WorkflowsViewList;

    /// <summary>The search box's placeholder.</summary>
    public string SearchPlaceholder => Labels.WorkflowsSearchRuns;

    /// <summary>The key hints beside the toolbar.</summary>
    public string KeyHints => Labels.WorkflowsKeyHints;

    /// <summary>The "Nothing here. No runs match this filter." line the list shows when a filter hides every run.</summary>
    public string NoRunsMatchText => Labels.WorkflowsNothingHere + " " + Labels.WorkflowsNoRunsMatch;
#pragma warning restore CA1822, MA0041

    /// <summary>"All 8".</summary>
    public string FilterAllText { get; private set => Set(ref field, value); } = Labels.WorkflowsFilterAll;

    /// <summary>"Needs you 2".</summary>
    public string FilterNeedsYouText { get; private set => Set(ref field, value); } = Labels.WorkflowsFilterNeedsYou;

    /// <summary>"Mine 3".</summary>
    public string FilterMineText { get; private set => Set(ref field, value); } = Labels.WorkflowsFilterMine;

    /// <summary>True when a filter or the search hides every run the page holds, in the view it draws.</summary>
    public bool NoRunsMatch { get; private set => Set(ref field, value); }

    /// <summary>True while the board draws its lanes: not when there are no runs at all, which says so in its place.</summary>
    public bool ShowBoard => IsBoard && !IsEmpty && _onBoard > 0;

    /// <summary>True when the board is chosen and no run lands in any lane: none at all.</summary>
    public bool IsBoardEmpty => IsBoard && (IsEmpty || _onBoard == 0);

    /// <summary>What the board says in place of its lanes: the served empty sentence with no runs at all, else the plain one.</summary>
    public string BoardEmptyText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the board is chosen and the last read could not be read; the lanes keep the last good runs, marked stale.</summary>
    public bool IsBoardUnreadable => IsBoard && IsUnreadable;

    /// <summary>Switches between the board and the list (the V key).</summary>
    internal void ToggleView() => SetViewMode(IsBoard ? WorkflowViewMode.List : WorkflowViewMode.Board);

    /// <summary>Chooses the board or the list.</summary>
    /// <param name="mode">The view.</param>
    internal void SetViewMode(WorkflowViewMode mode)
    {
        if (mode == ViewMode)
        {
            return;
        }

        ViewMode = mode;
        OnChanged(nameof(IsBoard));
        OnChanged(nameof(IsList));
        OnChanged(nameof(ViewIndex));
        NotifyBoardStates();
        Redraw();
        if (mode == WorkflowViewMode.Board && Url is not null && !string.Equals(_doneFor, DoneKey, StringComparison.Ordinal))
        {
            _ = ReadDone(CancellationToken.None);   // the Done lane fills from the finished read the list keeps behind its line, once for a filter
        }
    }

    /// <summary>Chooses a filter segment.</summary>
    /// <param name="filter">The segment.</param>
    internal void SetFilter(WorkflowFilter filter)
    {
        if (filter == Filter)
        {
            return;
        }

        Filter = filter;
        OnChanged(nameof(FilterIndex));
        Redraw();
    }

    /// <summary>Narrows what both views draw to the runs whose title, current step or reference holds the text.</summary>
    /// <param name="text">The text; blank for all.</param>
    internal void SetSearch(string? text)
    {
        var value = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (string.Equals(value, _search, StringComparison.Ordinal))
        {
            return;
        }

        _search = value;
        Redraw();
    }

    private void NotifyBoardStates()
    {
        OnChanged(nameof(ShowBoard));
        OnChanged(nameof(IsBoardEmpty));
        OnChanged(nameof(IsBoardUnreadable));
    }

    // The lane for runs outside the line is on the board only while it holds a run, just before Done.
    private void PlaceOtherLane(bool any)
    {
        var placed = Lanes.Contains(_otherLane);
        if (any && !placed)
        {
            Lanes.Insert(Lanes.Count - 1, _otherLane);
        }
        else if (!any && placed)
        {
            _ = Lanes.Remove(_otherLane);
        }
    }

    private static IEnumerable<InstanceRowViewModel> LaneCards(LaneId lane, List<InstanceRowViewModel> line, List<InstanceRowViewModel> finished, List<InstanceRowViewModel> other) => lane switch
    {
        LaneId.Done => finished,
        LaneId.Other => other,
        LaneId.Scoping or LaneId.Design or LaneId.Plan or LaneId.Build or LaneId.Merge => line.Where(r => LineStages.LaneOf(r.LineStage) == lane),
        _ => [],   // an undefined value, as LineStages.Name does
    };

    private bool Passes(InstanceRowViewModel row) => WorkflowFilters.Matches(row, Filter, _search);

    // The filter or the view changed: the loaded list is drawn again.
    private void Redraw()
    {
        if (_list is { } list)
        {
            Render(list);
        }
    }

    // The lanes: the line workflow's open runs by state, every other open run in its own lane, then the finished runs in Done; the segments' counts are over every open run.
    private void RebuildBoard()
    {
        var line = _loaded.Where(r => LineStages.LaneOf(r.LineStage) is not null).ToList();
        var finished = DoneRows.Where(r => r.LineStage is not null).ToList();
        var other = _loaded.Where(r => LineStages.LaneOf(r.LineStage) is null).ToList();
        _onBoard = line.Count + finished.Count + other.Count;
        var anyHidden = Filter != WorkflowFilter.All || _search is not null;
        PlaceOtherLane(other.Count > 0);
        foreach (var lane in Lanes)
        {
            lane.Show([.. LaneCards(lane.Id, line, finished, other).Where(Passes)], anyHidden);
        }

        // The chips count every open run in both views, as the header does, and the board draws each of them.
        var counted = _loaded;
        var (all, needs, mine) = WorkflowFilters.Counts(counted);
        FilterAllText = $"{Labels.WorkflowsFilterAll} {all.ToString(CultureInfo.InvariantCulture)}";
        FilterNeedsYouText = $"{Labels.WorkflowsFilterNeedsYou} {needs.ToString(CultureInfo.InvariantCulture)}";
        FilterMineText = $"{Labels.WorkflowsFilterMine} {mine.ToString(CultureInfo.InvariantCulture)}";
        NoRunsMatch = anyHidden && counted.Count > 0 && counted.TrueForAll(r => !Passes(r));
    }
}
