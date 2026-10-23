using Aw.Sindri.Ui.ViewModels.Confirmations;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The action bar (<see cref="NeedsYouBarViewModel"/>, continued): the typed and held confirmations of the three protected approvals the app
/// confirms (F3 needs-you spec §8.3). The controls approve; the bar only shows them, moves focus to them on A, and takes their answer.
/// </summary>
internal sealed partial class NeedsYouBarViewModel
{
    /// <summary>Raised when A asks to move to the confirmation control (the typed box's field, or the hold button).</summary>
    internal event EventHandler? ConfirmFocusRequested;

    /// <summary>The typed confirmation's state (the drifted profile's); null when the bar has none.</summary>
    public TypedConfirmBoxViewModel? TypedBox => _confirm?.TypedBox;

    /// <summary>The hold button's state (approve-all's, the dialog's own); null when the bar has none.</summary>
    public HoldButtonViewModel? HoldBox => _confirm?.Hold;

    /// <summary>True while the typed box shows (a drifted profile the catalog lets the app approve).</summary>
    public bool ShowsTyped => IsPending && ConfirmKind == NeedsYouConfirmKind.Typed;

    /// <summary>True while the hold button shows (approve-all, when the catalog lets the app approve).</summary>
    public bool ShowsHold => IsPending && ConfirmKind == NeedsYouConfirmKind.Hold;

    /// <summary>True when a confirmation control shows.</summary>
    public bool ShowsConfirm => ShowsTyped || ShowsHold;

    /// <summary>False while the request has left the list under the card: the controls go quiet.</summary>
    public bool ConfirmEnabled => Enabled;

    /// <summary>The merge stop's pull request card; null when the request has none.</summary>
    public MergeCardViewModel? MergeCard => _target?.MergeCard;

    /// <summary>True when the card shows.</summary>
    public bool HasMergeCard => MergeCard is not null;

    /// <summary>The step's served why for the shown desk request; empty when none is served.</summary>
    public string DeskWhy => _target?.Why ?? "";

    /// <summary>What approving does, as the step's recipe says; empty when not served.</summary>
    public string IfApprove => _target?.IfApprove ?? "";

    /// <summary>What sending it back does, as the step's recipe says; empty when not served.</summary>
    public string IfSendBack => _target?.IfSendBack ?? "";

    /// <summary>True when the approve box has words to show.</summary>
    public bool HasIfApprove => IfApprove.Length > 0;

    /// <summary>True when the send-back box has words to show.</summary>
    public bool HasIfSendBack => IfSendBack.Length > 0;

    private NeedsYouConfirmKind ConfirmKind => _confirm?.Kind ?? NeedsYouConfirmKind.None;

    /// <summary>The typed box was pressed on text that is exactly the challenge's: sends it, once.</summary>
    /// <param name="typed">The typed text.</param>
    /// <returns>A task that completes when the answer came.</returns>
    internal Task SubmitTyped(string typed) => _confirm is not null && Enabled && ShowsTyped ? _confirm.SubmitTyped(typed) : Task.CompletedTask;

    /// <summary>The hold reached the server's time: sends it, once.</summary>
    /// <param name="heldMs">How long the button was held.</param>
    /// <returns>A task that completes when the answer came.</returns>
    internal Task HoldReached(int heldMs) => _confirm is not null && Enabled && ShowsHold ? _confirm.Held(heldMs) : Task.CompletedTask;

    // A confirmation changed: its refusal shows in the bar, and an approval that went through is the bar's done card.
    private void OnConfirmChanged(object? sender, EventArgs e)
    {
        if (_confirm is not { } confirm)
        {
            return;
        }

        Refusal = confirm.Refusal;
        if (confirm.Done && Mode != NeedsYouBarMode.Approved)
        {
            (Mode, _done, ReasonText) = (NeedsYouBarMode.Approved, NeedsYouBarOutcome.Approve, "");
        }

        Refresh();
    }
}
