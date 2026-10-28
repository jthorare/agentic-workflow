using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>What the desk bar shows next to the answer (<see cref="NeedsYouBarViewModel"/>, continued; engine q65): a button for each thing the step asks the person to read, the agent session behind it, and why the step stands as it does.</summary>
internal sealed partial class NeedsYouBarViewModel
{
    /// <summary>Raised when a review has nothing to open: the shell takes the person to the instance's frame, where its facts are.</summary>
    internal event EventHandler<NeedsYouFactEventArgs>? FactRequested;

    /// <summary>Raised when Approve is pressed on a step that needs an output file: the bar has none to give, so the shell opens the row's sheet, which has the file inputs (#1170).</summary>
    internal event EventHandler<NeedsYouSheetEventArgs>? SheetRequested;

    /// <summary>Raised when the session link is followed; the shell opens the session's sheet.</summary>
    internal event EventHandler<NeedsYouSessionEventArgs>? SessionRequested;

    private void RequestSheet(string rowId)
    {
        (Mode, Refusal) = (NeedsYouBarMode.Pending, Labels.CompleteNeedsOutputInSheet);
        Refresh();
        SheetRequested?.Invoke(this, new NeedsYouSheetEventArgs(rowId));
    }

    /// <summary>A button for each thing the desk step asks the person to read (the design, the plan, the PR); empty when it asks none.</summary>
    public IReadOnlyList<DeskReviewViewModel> Reviews { get; private set; } = [];

    /// <summary>True when there is a review button.</summary>
    public bool HasReviews => Kind == BarKind.Desk && Reviews.Count > 0;

    /// <summary>True when the engine names the agent session that runs the step.</summary>
    public bool HasSession => Kind == BarKind.Desk && _target?.SessionRef is not null;

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The session link's words.</summary>
    public string SessionLinkText => Labels.SessionLink;
#pragma warning restore CA1822, MA0041

    /// <summary>The session link's accessible name.</summary>
    public string SessionLinkName => _target?.SessionRef is { } sessionRef ? Labels.SessionLinkName(sessionRef) : Labels.SessionLink;

    /// <summary>Why the step stands as it does; empty when the engine serves no code.</summary>
    public string ReasonLine => Kind == BarKind.Desk && _target?.ReasonCode is { Length: > 0 } code ? Labels.ReasonCodeWords(code) : string.Empty;

    /// <summary>True when there is a reason line.</summary>
    public bool HasReasonLine => ReasonLine.Length > 0;

    /// <summary>Opens the session's sheet; nothing without a named session.</summary>
    internal void OpenSession()
    {
        if (_target?.SessionRef is { } sessionRef)
        {
            SessionRequested?.Invoke(this, new NeedsYouSessionEventArgs(sessionRef));
        }
    }

    // The buttons for the run just read; cleared when another request shows.
    private void DrawReviews() =>
        Reviews = _target is { } target ? DeskReviewViewModel.Of(target.Reviews, _desk.Launcher, fact => FactRequested?.Invoke(this, new NeedsYouFactEventArgs(target.Address, fact))) : [];
}
