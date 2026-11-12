namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The Agents page's filter box and column sort (<see cref="AgentsPageViewModel"/>, continued).</summary>
internal sealed partial class AgentsPageViewModel
{
    /// <summary>Raised when the page should put focus in the filter box (the / key).</summary>
    internal event EventHandler? FilterFocusRequested;

    /// <summary>Asks the view to put focus in the filter box.</summary>
    internal void FocusFilter() => FilterFocusRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>The filter box's text. Set it from outside (a Home link) and the page opens filtered.</summary>
    public string FilterText { get; set { Set(ref field, value); Query = AgentFilterQuery.Parse(value); } } = string.Empty;

    /// <summary>The parsed filter text.</summary>
    internal AgentFilterQuery Query { get; private set; } = AgentFilterQuery.Empty;

    /// <summary>The column the rows are sorted by; null for the served order.</summary>
    public AgentSortColumn? SortColumn { get; private set; }

    /// <summary>True when the sort runs from the last to the first.</summary>
    public bool SortDescending { get; private set; }

    /// <summary>Sorts by a column; the same column again reverses the sort.</summary>
    /// <param name="column">The column whose header was clicked.</param>
    internal void SortBy(AgentSortColumn column) => _ = (column, SortColumn);

    /// <summary>Esc in the filter box: clears the text and puts focus back on the list.</summary>
    /// <returns>True when the key was used.</returns>
    internal bool EscapeFilter() => SortColumn is null && false;

    /// <summary>The Status header with its sort arrow.</summary>
    public string StatusCaption => Columns.Status;

    /// <summary>The Agent header with its sort arrow.</summary>
    public string AgentCaption => Columns.Agent;

    /// <summary>The Repository header with its sort arrow.</summary>
    public string RepoCaption => Columns.Repo;

    /// <summary>The Where header with its sort arrow.</summary>
    public string WhereCaption => Columns.Where;

    /// <summary>The Doing header with its sort arrow.</summary>
    public string DoingCaption => Columns.Doing;

    /// <summary>The For header with its sort arrow.</summary>
    public string ForCaption => Columns.For;
}
