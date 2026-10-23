namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The action bar (<see cref="NeedsYouBarViewModel"/>, continued): what A, D, Ctrl+Enter, Esc and the buttons do.</summary>
internal sealed partial class NeedsYouBarViewModel
{
    private static readonly TimeSpan _copiedShown = TimeSpan.FromMilliseconds(1500);

    /// <summary>A, or the primary button: approve, accept, or copy the terminal command. On a desk request not read to the end the first press only asks.</summary>
    /// <returns>A task that completes when the press was handled.</returns>
    internal async Task PressApprove()
    {
        if (!IsPending || !Enabled)
        {
            return;
        }

        if (ShowsConfirm)
        {
            ConfirmFocusRequested?.Invoke(this, EventArgs.Empty);   // a protected row's A only moves to its control: the hold or the typed box does the approving
        }
        else if (ShowsTerminal)
        {
            await CopyTerminal().ConfigureAwait(true);
        }
        else if (Kind == BarKind.Decision)
        {
            await AcceptDecision().ConfigureAwait(true);
        }
        else if (Kind == BarKind.Desk && PrimaryEnabled)
        {
            await ApproveDesk().ConfigureAwait(true);
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

    // The first A on a request not read to the end only asks; the second, or one on a request read to the end, reports it.
    private async Task ApproveDesk()
    {
        if (HasQuestions)
        {
            if (AllAnswered)
            {
                await Report(NeedsYouBarOutcome.Answer, DeskReporter.AnswersJson(Questions.Select(q => (q.Question.Id, q.Question.Question, q.Answer.Trim())))).ConfigureAwait(true);
            }

            return;   // every answer is needed; the question box is the consent, so there is no "read to the end" step
        }

        if (!ReadToEnd && Mode != NeedsYouBarMode.Confirming)
        {
            Mode = NeedsYouBarMode.Confirming;
            Refresh();
            return;
        }

        await Report(NeedsYouBarOutcome.Approve, answers: null).ConfigureAwait(true);
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
        else if (Kind == BarKind.Desk)
        {
            await Report(TerminalOnly ? NeedsYouBarOutcome.Rework : NeedsYouBarOutcome.Revise, reason).ConfigureAwait(true);
        }
    }

    /// <summary>Esc, or Cancel: closes the reason box, the drop question or the "Approve?" question. What was typed stays.</summary>
    internal void Cancel()
    {
        if (HasOpenQuestion)
        {
            Mode = NeedsYouBarMode.Pending;
            Refresh();
        }
    }

    /// <summary>The More menu's "Drop this item": asks to confirm first.</summary>
    /// <returns>A task that completes when the question shows.</returns>
    internal Task RequestDrop()
    {
        if (IsPending && ShowsDrop && Enabled)
        {
            Mode = NeedsYouBarMode.ConfirmingDrop;
            Refresh();
        }

        return Task.CompletedTask;
    }

    /// <summary>The drop question's button: sends the drop.</summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task ConfirmDrop()
    {
        if (IsConfirmingDrop && Enabled)
        {
            await Report(NeedsYouBarOutcome.Drop, answers: null).ConfigureAwait(true);
        }
    }

    /// <summary>Copies the served command the terminal button stands for.</summary>
    /// <returns>A task that completes when the clipboard answered.</returns>
    internal async Task CopyTerminal()
    {
        if (!ShowsTerminal || TerminalCli.Length == 0)
        {
            return;
        }

        if (await _clipboard.SetText(TerminalCli, CancellationToken.None).ConfigureAwait(true))
        {
            HintIsSuccess = true;
            Refresh();
            CopiedExpiry = DropCopied();
        }
    }

    private async Task DropCopied()
    {
        await Task.Delay(_copiedShown, _clock).ConfigureAwait(true);
        HintIsSuccess = false;
        Refresh();
    }

    // One report per press: the flag is set before the first await. A refusal shows as served and leaves the request pressable.
    private async Task Report(NeedsYouBarOutcome outcome, string? answers)
    {
        if (_target is not { } target || IsSending || _row is null)
        {
            return;
        }

        var shown = _row.Id;
        (IsSending, Refusal) = (true, "");
        Refresh();
        var answer = await _desk.Complete(target, WordOf(outcome), answers).ConfigureAwait(true);
        if (_row is null || !string.Equals(_row.Id, shown, StringComparison.Ordinal))
        {
            return;   // another request is shown now, and it owns the flag
        }

        IsSending = false;
        if (answer is { IsError: true } refused)
        {
            Refusal = refused.Error.Message;
            Mode = NeedsYouBarMode.Pending;
        }
        else if (answer is not null)
        {
            (Mode, _done, ReasonText) = (NeedsYouBarMode.Approved, outcome, "");
        }

        Refresh();
    }

    // The outcome ids the desk steps declare (spec §5): the app sends the step's own words.
    private static string WordOf(NeedsYouBarOutcome outcome) => outcome switch
    {
        NeedsYouBarOutcome.Approve => "approve",
        NeedsYouBarOutcome.Revise => "revise",
        NeedsYouBarOutcome.Rework => "rework",
        NeedsYouBarOutcome.Drop => "drop",
        NeedsYouBarOutcome.Answer => "done",
        NeedsYouBarOutcome.None => throw new InvalidOperationException("no outcome to send"),
        _ => throw new InvalidOperationException("an outcome the bar doesn't know"),
    };
}
