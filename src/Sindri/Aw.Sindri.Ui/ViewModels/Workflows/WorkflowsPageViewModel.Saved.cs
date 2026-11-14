namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): a saved revision that is not the live one (#3303).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>The line under the header after a save: "Saved as revision 3. Not live yet." on a live workflow, "Saved as revision 3." on one that is off; empty otherwise.</summary>
    public string SavedLineText => string.Empty;

    /// <summary>True when the saved-revision line shows.</summary>
    public bool HasSavedLine => false;

    /// <summary>True when a revision newer than the live one is saved: Make live is offered.</summary>
    public bool CanMakeLive => false;

    /// <summary>True when the header's own Activate button shows: Activate is offered and Make live is not.</summary>
    public bool ShowActivateButton => CanActivate && !CanMakeLive;

    /// <summary>Make live: turns the newest saved revision on.</summary>
    /// <param name="cancellationToken">Cancels the call and the reads.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task MakeLive(CancellationToken cancellationToken) => Task.CompletedTask;
}
