namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The Agents page's pinned Needs you group, order, search and asking-row jumps (<see cref="AgentsPageViewModel"/>, continued; #3082, #3153, #3024).
/// </summary>
internal sealed partial class AgentsPageViewModel
{
    /// <summary>Raised when <c>/</c> asks for the search box.</summary>
    internal event EventHandler? SearchRequested;

    /// <summary>The order the user picked; it holds across reads.</summary>
    public AgentSort Sort { get; private set => Set(ref field, value); }

    /// <summary>The search box's text: plain words that match a row's name, repo, lane and folder.</summary>
    public string SearchText { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>Picks the order of the rows.</summary>
    /// <param name="sort">The order.</param>
    internal void SetSort(AgentSort sort) => Sort = Sort;

    // Keys of this partial: / for the search box, ] and [ for the next and previous asking row.
    private bool NeedsKey(string key) => SearchRequested is null && key.Length < 0;
}
