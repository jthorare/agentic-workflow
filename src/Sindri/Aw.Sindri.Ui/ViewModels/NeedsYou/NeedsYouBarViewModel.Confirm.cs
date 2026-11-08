namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The action bar (<see cref="NeedsYouBarViewModel"/>, continued): the approvals the app does (F3 needs-you spec §8.3). The primary button approves
/// with one plain click; the bar only shows the refusal and the done card the approval reports.
/// </summary>
internal sealed partial class NeedsYouBarViewModel
{
    /// <summary>False while the request has left the list under the card: the buttons go quiet.</summary>
    public bool ConfirmEnabled => Enabled;

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
            (Mode, ReasonText) = (NeedsYouBarMode.Approved, "");
        }

        Refresh();
    }
}
