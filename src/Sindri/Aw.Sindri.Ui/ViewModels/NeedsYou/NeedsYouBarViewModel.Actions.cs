namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The action bar (<see cref="NeedsYouBarViewModel"/>, continued): what A, D, Ctrl+Enter, Esc and the buttons do.</summary>
internal sealed partial class NeedsYouBarViewModel
{
    /// <summary>A, or the primary button: approve or accept. </summary>
    /// <returns>A task that completes when the press was handled.</returns>
    internal async Task PressApprove()
    {
        if (!IsPending || !Enabled)
        {
            return;
        }

        if (Kind == BarKind.Settings)
        {
            if (PrimaryEnabled && _confirm is { } confirm)
            {
                await confirm.Approve().ConfigureAwait(true);
            }
        }
        else if (Kind == BarKind.Decision)
        {
            await AcceptDecision().ConfigureAwait(true);
        }
    }

    private async Task AcceptDecision()
    {
        if (AcceptControl is { IsEnabled: true } accept)
        {
            await Decide(accept.Action.OptionId, "").ConfigureAwait(true);
        }
    }

    // The shell answers the decision as its sheet would; the reason stays until it is accepted, and a refusal shows here, not only in a sheet this page doesn't draw.
    private async Task Decide(string optionId, string reason)
    {
        if (_row is null || IsSending)
        {
            return;
        }

        var (shown, asked) = (_row.Id, new NeedsYouDecisionEventArgs(_row.Id, optionId, reason));
        (IsSending, Refusal) = (true, "");
        Refresh();
        DecisionRequested?.Invoke(this, asked);
        var refusal = await asked.Settled.ConfigureAwait(true);
        if (_row is null || !string.Equals(_row.Id, shown, StringComparison.Ordinal))
        {
            return;
        }

        IsSending = false;
        if (refusal.Length > 0)
        {
            Refusal = refusal;
        }
        else
        {
            (Mode, ReasonText) = (NeedsYouBarMode.Pending, "");
        }

        Refresh();
    }

    /// <summary>D, or the send back button: opens the reason box. A settings request has no send back, so D does nothing there.</summary>
    /// <returns>A task that completes when the press was handled.</returns>
    internal Task PressSendBack()
    {
        if (IsPending && SendBackEnabled)
        {
            Mode = NeedsYouBarMode.SendingBack;
            Refresh();
            ReasonRequested?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    /// <summary>Ctrl+Enter, or the send button: sends the reason with the send back or the decline. Nothing is sent without text.</summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task Send()
    {
        if (!CanSend || _row is null)
        {
            return;
        }

        var reason = ReasonText.Trim();
        if (Kind == BarKind.Decision && RejectControl is { IsEnabled: true } reject)
        {
            await Decide(reject.Action.OptionId, reason).ConfigureAwait(true);
        }
    }

    /// <summary>Esc, or Cancel: closes the reason box or the "Approve?" question. What was typed stays.</summary>
    internal void Cancel()
    {
        if (HasOpenQuestion)
        {
            Mode = NeedsYouBarMode.Pending;
            Refresh();
        }
    }
}
