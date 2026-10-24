namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The scoping interview on the desk bar (<see cref="NeedsYouBarViewModel"/>, continued): each question a choice among its answers, Other or Skip; a round is one more batch in "fact:answers".</summary>
internal sealed partial class NeedsYouBarViewModel
{
    private bool _composing;

    /// <summary>The interview's questions, each with its choices; empty for every other request.</summary>
    public IReadOnlyList<ScopingQuestionViewModel> Scoping { get; private set; } = [];

    /// <summary>True while the card asks the scoping interview's questions.</summary>
    public bool HasScoping => Kind == BarKind.Desk && Scoping.Count > 0;

    /// <summary>True when the card asks questions of either kind: the answers are what the step needs.</summary>
    public bool HasAsk => HasQuestions || HasScoping;

    /// <summary>True when the questions asked have all the answers they need.</summary>
    public bool AskReady => HasScoping ? ScopingReady : AllAnswered;

    /// <summary>True when every question has a pick that counts.</summary>
    public bool ScopingReady => Scoping.All(q => q.IsAnswered);

    /// <summary>What Send answers waits for, while a question has no pick; empty otherwise.</summary>
    public string ScopingHint => HasScoping && !ScopingReady ? Labels.ScopingChooseAll : string.Empty;

#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>The "Done asking" button's words.</summary>
    public string DoneAskingWord => Labels.ScopingDoneAsking;
#pragma warning restore CA1822, MA0041

    /// <summary>True when "Done asking" shows.</summary>
    public bool ShowsDoneAsking => HasScoping;

    /// <summary>True when "Done asking" can be pressed.</summary>
    public bool DoneAskingEnabled => HasScoping && Enabled && !IsSending && !_composing && _target is not null;

    /// <summary>Done asking: sends what is answered so far as the last round and ends the interview.</summary>
    /// <returns>A task that completes when the answer came.</returns>
    internal async Task PressDoneAsking()
    {
        if (DoneAskingEnabled)
        {
            await SendRound(NeedsYouBarOutcome.DoneAsking).ConfigureAwait(true);
        }
    }

    // One round: the answers so far are read, this round's batch is added at the end and the whole array is written back, once.
    private async Task SendRound(NeedsYouBarOutcome outcome)
    {
        if (_target is not { } target || IsSending || _composing || _row is null)
        {
            return;
        }

        _composing = true;
        Refusal = "";
        Refresh();
        try
        {
            var existing = await _desk.ReadAnswers(target).ConfigureAwait(true);
            var written = existing.IsSuccess
                ? ScopingAnswers.Append(existing.Value, [.. Scoping.Select(q => q.Question.Id)], [.. Scoping.Select(q => q.Entry()).OfType<System.Text.Json.Nodes.JsonObject>()], _clock.GetUtcNow())
                : null;
            if (written is null)
            {
                Refusal = existing.IsError ? existing.Error.Message : Labels.ScopingAnswersUnreadable;   // nothing is sent over answers that could not be read
                return;
            }

            _composing = false;
            await Report(outcome, written).ConfigureAwait(true);
        }
        finally
        {
            _composing = false;
            Refresh();
        }
    }
}
