namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The Agents page's filter box (<see cref="AgentsPageViewModel"/>, continued; issue 3024): the text narrows the one list that
/// was read, in the app. The head names the shown agents out of the served count while the text narrows the list.
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

    private bool Matches(Api.Sessions.SessionRowView row) => Query.IsEmpty || Query.Matches(SessionRowViewModel.TargetOf(row));

    private void Redraw()
    {
        if (_list is { } list)
        {
            Apply(WalkKeys(), list);
        }
    }
}
