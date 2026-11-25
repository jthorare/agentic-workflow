namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): Done and Escape in edit mode ask "Save your changes?" before they drop typed fields (#3655).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>True while Done asks "Save your changes?" because a field of the open sheet changed.</summary>
    public bool AsksSaveFirst { get; private set { Set(ref field, value); OnChanged(nameof(ShowsEditorButtons)); } }

    /// <summary>True when the sheet's own Save and Close row shows: not while the question, which has its own Save, is up.</summary>
    public bool ShowsEditorButtons => !AsksSaveFirst;

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The question's words.</summary>
    public string AskSaveText => Labels.WfAskSave;

    /// <summary>The question's Discard button.</summary>
    public string AskDiscardText => Labels.WfAskDiscard;

    /// <summary>The question's Keep editing button.</summary>
    public string AskKeepText => Labels.WfAskKeep;
#pragma warning restore CA1822, MA0041

    private bool _leaveWhenSaved;

    /// <summary>Done (and Escape in edit mode with the sheet shut): leaves edit mode, or asks first when a field of the open sheet changed. A save in flight is waited for, and its result decides.</summary>
    internal void RequestDone()
    {
        if (Editor.IsSending)
        {
            _leaveWhenSaved = true;
            return;
        }

        if (IsSettingsOpen && Editor.Fields.Any(static f => f.IsChanged))
        {
            AsksSaveFirst = true;
            return;
        }

        CloseEditor();
    }

    /// <summary>Escape with the sheet open: asks like Done when a field changed, else closes the sheet (edit mode stays on).</summary>
    internal void RequestCloseSheet()
    {
        if (Editor.Fields.Any(static f => f.IsChanged))
        {
            AsksSaveFirst = true;
            return;
        }

        CloseSettings();
    }

    /// <summary>The question's Discard: leaves edit mode and drops the typed fields.</summary>
    internal void DiscardAndLeave()
    {
        AsksSaveFirst = false;
        CloseEditor();
    }

    /// <summary>The question's Keep editing: back to the sheet, text kept.</summary>
    internal void KeepEditing() => AsksSaveFirst = false;

    /// <summary>The question's Save: saves, and leaves edit mode only when the save worked; a failed save keeps the sheet and its error.</summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>A task that completes when the save is answered.</returns>
    internal async Task SaveAndLeave(CancellationToken cancellationToken)
    {
        if (!IsSettingsOpen || !CanSave)
        {
            return;   // Save would send nothing (a required field is empty, a one-of is not met): the question stays, and leaving would drop the typed text
        }

        AsksSaveFirst = false;
        await Save(cancellationToken).ConfigureAwait(true);
        LeaveIfSaved();
    }

    // The save worked when the sheet shows no failure and no reload prompt.
    private void LeaveIfSaved()
    {
        if (Editor.FailureText.Length == 0 && !Editor.NeedsReload)
        {
            CloseEditor();
        }
    }
}
