namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The Agents page's filter box and column sort (<see cref="AgentsPageViewModel"/>, continued; issue 3024): the text narrows the one list that
/// was read, in the app; a header click sorts the rows inside each group. The head names the shown agents out of the served count while the text narrows the list.
/// </summary>
internal sealed partial class AgentsPageViewModel
{
    /// <summary>Raised when the page should put focus in the filter box (the / key).</summary>
    internal event EventHandler? FilterFocusRequested;

    /// <summary>
    /// The filter box's text: words and the tokens p: r: s: n: o: (<see cref="AgentFilterQuery"/>). Set it from outside, such as a Home link
    /// (shell.Agents.FilterText = "s:asking"), and the page opens filtered, even before its first read.
    /// </summary>
    public string FilterText
    {
        get;
        set
        {
            Set(ref field, value ?? string.Empty);
            Query = AgentFilterQuery.Parse(field);
            OnChanged(nameof(IsFiltered));
            Redraw();
        }
    } = string.Empty;

    /// <summary>The parsed filter text.</summary>
    internal AgentFilterQuery Query { get; private set; } = AgentFilterQuery.Empty;

    /// <summary>The column the rows are sorted by; null for the served order.</summary>
    public AgentSortColumn? SortColumn { get; private set; }

    /// <summary>True when the sort runs from the last to the first.</summary>
    public bool SortDescending { get; private set; }

    /// <summary>The Status header with its sort arrow.</summary>
    public string StatusCaption => Caption(AgentSortColumn.Status, Columns.Status);

    /// <summary>The Agent header with its sort arrow.</summary>
    public string AgentCaption => Caption(AgentSortColumn.Agent, Columns.Agent);

    /// <summary>The Repository header with its sort arrow.</summary>
    public string RepoCaption => Caption(AgentSortColumn.Repo, Columns.Repo);

    /// <summary>The Where header with its sort arrow.</summary>
    public string WhereCaption => Caption(AgentSortColumn.Where, Columns.Where);

    /// <summary>The Doing header with its sort arrow.</summary>
    public string DoingCaption => Caption(AgentSortColumn.Doing, Columns.Doing);

    /// <summary>The For header with its sort arrow.</summary>
    public string ForCaption => Caption(AgentSortColumn.For, Columns.For);

    /// <summary>Asks the view to put focus in the filter box.</summary>
    internal void FocusFilter() => FilterFocusRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Esc in the filter box: clears the text and puts focus back on the list (the selected row, else the first).</summary>
    /// <returns>True: the key was used.</returns>
    internal bool EscapeFilter()
    {
        FilterText = string.Empty;
        var rows = Walk();
        var target = rows.Find(w => string.Equals(w.Key, SelectedKey, StringComparison.Ordinal));
        if (target.Key is null && rows.Count > 0)
        {
            target = rows[0];
        }

        if (target.Key is not null)
        {
            RequestFocus(new AgentFocusEventArgs(target.Key, toFocusLine: false, onlyIfLost: false));
        }

        return true;
    }

    /// <summary>Sorts by a column; the same column again reverses the sort.</summary>
    /// <param name="column">The column whose header was clicked.</param>
    internal void SortBy(AgentSortColumn column)
    {
        SortDescending = SortColumn == column && !SortDescending;
        SortColumn = column;
        foreach (var name in (string[])[nameof(SortColumn), nameof(SortDescending), nameof(StatusCaption), nameof(AgentCaption), nameof(RepoCaption), nameof(WhereCaption), nameof(DoingCaption), nameof(ForCaption)])
        {
            OnChanged(name);
        }

        Redraw();
    }

    private string Caption(AgentSortColumn column, string text) => SortColumn == column ? text + (SortDescending ? " ▼" : " ▲") : text;

    private bool Matches(Api.Sessions.SessionRowView row) => Query.IsEmpty || Query.Matches(SessionRowViewModel.TargetOf(row));

    private List<SessionRowViewModel> Sorted(List<SessionRowViewModel> rows)
    {
        if (SortColumn is not { } column)
        {
            return rows;
        }

        var sign = SortDescending ? -1 : 1;
        return [.. rows.Order(Comparer<SessionRowViewModel>.Create((a, b) => sign * Compare(column, a, b)))];
    }

    private static int Compare(AgentSortColumn column, SessionRowViewModel a, SessionRowViewModel b) => column switch
    {
        AgentSortColumn.Status => Rank(a.Row.State).CompareTo(Rank(b.Row.State)),
        AgentSortColumn.Agent => Text(a.TitleText, b.TitleText),
        AgentSortColumn.Repo => Text(a.Row.Repo ?? string.Empty, b.Row.Repo ?? string.Empty),
        AgentSortColumn.Where => Text(a.WhereText, b.WhereText),
        AgentSortColumn.Doing => Text(a.AskingText + a.StepText, b.AskingText + b.StepText),
        AgentSortColumn.For => b.Row.Since.CompareTo(a.Row.Since),   // the shortest time in the status first
        _ => 0,
    };

    // The sessions that need you first, then the ones at work, the idle, the rest.
    private static int Rank(string state) => state switch { "asking" => 0, "running" => 1, "idle" => 2, _ => 3 };

    private static int Text(string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase);

    private void Redraw()
    {
        if (_list is { } list)
        {
            Apply(WalkKeys(), list);
        }
    }
}
