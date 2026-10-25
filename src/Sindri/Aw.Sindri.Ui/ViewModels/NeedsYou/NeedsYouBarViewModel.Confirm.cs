namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The action bar (<see cref="NeedsYouBarViewModel"/>, continued): the three approvals the app does (F3 needs-you spec §8.3). The primary button approves
/// with one plain click; the bar only shows the refusal and the done card the approval reports.
/// </summary>
internal sealed partial class NeedsYouBarViewModel
{
    /// <summary>False while the request has left the list under the card: the buttons go quiet.</summary>
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
