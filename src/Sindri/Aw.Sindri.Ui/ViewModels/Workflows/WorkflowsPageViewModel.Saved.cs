namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): a saved revision that is not the live one (#3303).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    private int _savedWhileOff;   // the revision a save made on a workflow that is off; 0 until one did

    // the live revision is older than the newest one: an edit made a revision that is saved but not running
    private bool IsNotLive => IsRuns && Runs.Shown is { ActiveRevision: { } active } shown && active < shown.LatestRevision;

    /// <summary>The line under the header after a save: "Saved as revision 3. Not live yet." on a live workflow, "Saved as revision 3." on one that is off; empty otherwise.</summary>
    public string SavedLineText => IsNotLive
        ? Labels.WfSavedNotLive(Runs.Shown!.LatestRevision)   // why: IsNotLive holds, so Shown is set
        : IsRuns && _savedWhileOff > 0 && Runs.Shown is { ActiveRevision: null } ? Labels.WfSaved(_savedWhileOff) : string.Empty;

    /// <summary>True when the saved-revision line shows.</summary>
    public bool HasSavedLine => SavedLineText.Length > 0;

    /// <summary>True when a revision newer than the live one is saved and no call is on the way: Make live is offered.</summary>
    public bool CanMakeLive => IsNotLive && !IsChangingState;

    /// <summary>True when the header's own Activate button shows: Activate is offered and Make live is not.</summary>
    public bool ShowActivateButton => CanActivate && !CanMakeLive;

    /// <summary>The Make live button's words, "Making live…" while its call is on the way.</summary>
    public string MakeLiveText => IsChangingState ? Labels.WfMakeLiveBusy : Labels.WfMakeLive;

    /// <summary>Make live: turns the newest saved revision on. Sent once; the list and the runs are read again after any answer that reached Sindri.</summary>
    /// <param name="cancellationToken">Cancels the call and the reads.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task MakeLive(CancellationToken cancellationToken) => ChangeState(CanMakeLive, ActivateCall, Labels.WfActivateRefused, cancellationToken);

    // a save on a workflow that is off is the one case the revisions alone can't tell: remember it for the line
    private void NoteSaved()
    {
        _savedWhileOff = Runs.Shown is { ActiveRevision: null } shown ? shown.LatestRevision : 0;
        NotifySaved();
    }

    private void NotifySaved()
    {
        OnChanged(nameof(SavedLineText));
        OnChanged(nameof(HasSavedLine));
        OnChanged(nameof(CanMakeLive));
        OnChanged(nameof(ShowActivateButton));
        OnChanged(nameof(MakeLiveText));
    }
}
