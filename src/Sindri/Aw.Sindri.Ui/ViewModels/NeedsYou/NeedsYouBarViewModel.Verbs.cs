namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The action bar (<see cref="NeedsYouBarViewModel"/>, continued): one button per recipe verb the instance accepts now (the pull request babysitter's
/// stop 3: held comments, resync docs, check now, send back to rework, merge). The merge asks first, in the Destructive style, and is sent as the served desk
/// action; no terminal copy stands in for it. A verb the app has no words for is shown as served.
/// </summary>
internal sealed partial class NeedsYouBarViewModel
{
    /// <summary>The verb buttons, in the order the instance serves them; empty when it serves none.</summary>
    public IReadOnlyList<DeskVerbViewModel> Verbs { get; private set => Set(ref field, value); } = [];

    /// <summary>True when the instance serves at least one recipe verb.</summary>
    public bool HasVerbs => Verbs.Count > 0;

    /// <summary>True when the instance serves the merge: the app sends it, so no terminal copy is offered.</summary>
    public bool HasMergeVerb => Verbs.Any(v => string.Equals(v.Verb, "merge", StringComparison.Ordinal));

    /// <summary>True when the verbs can be pressed now.</summary>
    public bool VerbsEnabled => Enabled && !IsSending && _target is not null;

    /// <summary>What the bar says after a verb other than the merge was sent; empty otherwise.</summary>
    public string VerbNote { get; private set => Set(ref field, value); } = string.Empty;

    // The target was read (or dropped): the buttons follow what it serves.
    private void DrawVerbs()
    {
        var subject = Subject;
        var shown = _target?.Verbs.Select(v => new DeskVerbViewModel(v, subject, this)).ToList() ?? [];
        Verbs = shown;
        VerbNote = string.Empty;
        OnChanged(nameof(HasVerbs));
        OnChanged(nameof(HasMergeVerb));
        OnChanged(nameof(VerbsEnabled));
    }

    /// <summary>A verb button's press: a destructive verb asks first, any other is sent at once.</summary>
    /// <param name="verb">The button.</param>
    /// <returns>A task that completes when the press was handled.</returns>
    internal Task PressVerb(DeskVerbViewModel verb)
    {
        if (!VerbsEnabled)
        {
            return Task.CompletedTask;
        }

        if (verb.IsDestructive)
        {
            foreach (var other in Verbs)
            {
                other.IsConfirming = ReferenceEquals(other, verb);
            }

            return Task.CompletedTask;
        }

        return SendVerb(verb);
    }

    /// <summary>The question's confirm: sends the verb.</summary>
    /// <param name="verb">The button whose question was answered.</param>
    /// <returns>A task that completes when the answer came.</returns>
    internal Task ConfirmVerb(DeskVerbViewModel verb) => verb.IsConfirming ? SendVerb(verb) : Task.CompletedTask;

    // One send per press: the flag is set before the first await. A refusal shows as served and leaves the verbs pressable.
    private async Task SendVerb(DeskVerbViewModel verb)
    {
        if (_target is not { } target || IsSending || _row is null)
        {
            return;
        }

        var shown = _row.Id;
        (IsSending, Refusal, verb.IsConfirming) = (true, "", false);
        Refresh();
        var answer = await _desk.ApplyVerb(target, verb.Verb).ConfigureAwait(true);
        if (_row is null || !string.Equals(_row.Id, shown, StringComparison.Ordinal))
        {
            return;   // another request is shown now, and it owns the flag
        }

        IsSending = false;
        if (answer is { IsError: true } refused)
        {
            Refusal = refused.Error.Message;
        }
        else if (answer is not null && string.Equals(verb.Verb, "merge", StringComparison.Ordinal))
        {
            (Mode, _done, ReasonText) = (NeedsYouBarMode.Approved, NeedsYouBarOutcome.Approve, "");
        }
        else if (answer is not null)
        {
            VerbNote = Labels.VerbSent(verb.Label);
        }

        Refresh();
    }
}
