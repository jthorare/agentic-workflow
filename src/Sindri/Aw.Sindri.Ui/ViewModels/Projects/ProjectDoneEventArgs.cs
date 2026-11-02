namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The sheet's write took its card off the page, or back: the shell closes the sheet and the page moves focus.</summary>
/// <param name="kind">What was done.</param>
/// <param name="id">The card's id.</param>
/// <param name="said">The sentence to announce ("Checkout rewrite hidden").</param>
/// <param name="wentToFinished">For an unhide: the project is Done or Canceled, so it is back under Finished.</param>
internal sealed class ProjectDoneEventArgs(ProjectDone kind, string id, string said, bool wentToFinished) : EventArgs
{
    /// <summary>What was done.</summary>
    public ProjectDone Kind { get; } = kind;

    /// <summary>The card's id.</summary>
    public string Id { get; } = id;

    /// <summary>The sentence to announce.</summary>
    public string Said { get; } = said;

    /// <summary>True when an unhidden project went under Finished.</summary>
    public bool WentToFinished { get; } = wentToFinished;
}
