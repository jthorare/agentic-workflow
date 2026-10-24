namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>How one action of a row is drawn: the action, whether it is the row's primary, whether it takes input, and its tooltip.</summary>
/// <param name="Action">The served action.</param>
/// <param name="IsPrimary">True for the row's primary control (drawn first).</param>
/// <param name="IsEnabled">True when it takes input: ready, and the views aren't rebuilding.</param>
/// <param name="Tooltip">Why it can't be used (the served command of a pending action, retry after the rebuild); empty when it can.</param>
/// <param name="SubjectId">The id the row shows, so the control's name says which row it acts on.</param>
internal sealed record NeedsYouActionControl(NeedsYouAction Action, bool IsPrimary, bool IsEnabled, string Tooltip, string SubjectId = "")
{
    /// <summary>The accessible name: the label and the row's id (a list of buttons has several "Approve").</summary>
    internal string Name => SubjectId.Length > 0 ? Action.Label + " " + SubjectId : Action.Label;
}
