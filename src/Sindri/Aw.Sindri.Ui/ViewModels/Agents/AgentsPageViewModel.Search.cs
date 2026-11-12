namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The Agents page's filter box (<see cref="AgentsPageViewModel"/>, continued).</summary>
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

    /// <summary>Esc in the filter box: clears the text and puts focus back on the list.</summary>
    /// <returns>True when the key was used.</returns>
    internal bool EscapeFilter() => throw new NotSupportedException();
}
