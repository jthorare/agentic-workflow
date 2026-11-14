namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>Whether a step's body has anything to show, and so whether a failed row opens (#3616).</summary>
internal sealed partial class SetupStepViewModel
{
    /// <summary>True when the open row would show something: any part of the body in SetupStepRow.axaml (link, detail, meter, repo lines, preview, red band, hint, agents, repo form, note, button).</summary>
    public bool HasBody => HasTextBody || HasControlBody;

    // The text parts of the body: link, detail, hint, red band, note (split from HasBody to stay under CA1502).
    private bool HasTextBody => HasLink || HasDetail || HasHint || HasError || HasNote;

    // The control parts of the body: meter, repo lines, preview, agents, repo form, button.
    private bool HasControlBody => HasProgress || HasItems || HasPreview || HasAgents || Repo is not null || HasAction;

    /// <summary>True when the row opens by itself: a step that needs the person, or a failed step that has a body.</summary>
    private bool Opens => NeedsYou || (IsFailed && HasBody);

    /// <summary>Opens a failed row that has just gained a body (the repo form is attached after the row is made).</summary>
    internal void OpenIfFailedWithBody()
    {
        OnChanged(nameof(HasBody));
        if (IsFailed && HasBody)
        {
            IsOpen = true;
        }
    }

    // Called by Update: opens a row that moved into needs-you or failed, or a failed row that gained a body.
    private void OpenOnUpdate(bool stateChanged, bool hadBody)
    {
        if (Opens && (stateChanged || (IsFailed && !hadBody)))
        {
            IsOpen = true;
        }
    }
}
