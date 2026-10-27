using System.Text.RegularExpressions;

namespace Aw.Judge.Core.Transcripts;

/// <summary>
/// What the user did next, read from a transcript: the outcome labels of the 'ask-check' and 'turn-progress' items. Free of
/// any provider call. Transcript text is untrusted data here: it is only matched and counted, never followed.
/// </summary>
public static partial class TurnOutcomes
{
    /// <summary>The 'turn-progress' label of a turn whose edits mostly lived.</summary>
    public const string Progressing = "progressing";

    /// <summary>The 'turn-progress' label of a turn whose edits were mostly undone, interrupted or corrected.</summary>
    public const string Stalled = "stalled";

    private const string Continue = "continue";
    private const string Ask = "ask";
    private const string Interrupt = "[Request interrupted by user";
    private const string CompactionSummary = "This session is being continued from a previous conversation";
    private const int TimeoutMs = 1000;
    private static readonly TimeSpan _sixHours = TimeSpan.FromHours(6);

    private enum Reply
    {
        Other,
        Interrupt,
        Correction,
        BareContinue,
    }

    /// <summary>Whether text is a real prompt: machine text (system reminders, command wrappers, the summary Claude Code injects after a compaction) is not.</summary>
    /// <param name="text">The user message text.</param>
    /// <returns>True for a prompt the user typed.</returns>
    public static bool IsRealPromptText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.TrimStart();
        return !trimmed.StartsWith('<') && !trimmed.StartsWith(CompactionSummary, StringComparison.Ordinal);
    }

    /// <summary>The label of an 'ask-check' decision from the next real prompt after it.</summary>
    /// <param name="action">What the judge decided, 'continue' or 'ask'.</param>
    /// <param name="decidedAt">When it was decided.</param>
    /// <param name="prompts">The session's real prompts, in order.</param>
    /// <returns>'continue' or 'ask' (the answer that was right), or null when there is no next prompt within six hours or the action is neither.</returns>
    public static string? AskCheckLabel(string action, DateTimeOffset decidedAt, IReadOnlyList<TranscriptPrompt> prompts)
    {
        ArgumentNullException.ThrowIfNull(prompts);
        var next = prompts.FirstOrDefault(p => p.At > decidedAt);
        if (next is null || next.At - decidedAt > _sixHours)
        {
            return null;
        }

        var reply = Classify(next.Text);
        return action switch
        {
            Continue => reply is Reply.Interrupt or Reply.Correction ? Ask : Continue,
            Ask => reply is Reply.BareContinue ? Continue : Ask,
            _ => null,
        };
    }

    /// <summary>The label of one turn's edits: line survival first, then the next real prompt.</summary>
    /// <param name="session">One session's turns, in order.</param>
    /// <param name="index">The turn to label (its position in <paramref name="session"/>).</param>
    /// <param name="sessionClosed">Whether the session is over, so the last turn has no next prompt to wait for.</param>
    /// <returns>'progressing' or 'stalled', or null when the turn has no signal yet.</returns>
    public static string? TurnLabel(IReadOnlyList<TranscriptTurn> session, int index, bool sessionClosed)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (Survival(session, index) is not { } survival)
        {
            return null;
        }

        var next = index + 1 < session.Count ? session[index + 1] : null;
        if (next is null && !sessionClosed)
        {
            return null;
        }

        var reply = next is null ? Reply.Other : Classify(next.Prompt);
        return survival < 0.3 || reply is Reply.Interrupt or Reply.Correction
            ? Stalled
            : survival >= 0.7 ? Progressing : null;
    }

    /// <summary>A turn's edits as a diff-like text: a '--- file' header, then the removed lines with '- ' and the added lines with '+ '.</summary>
    /// <param name="edits">The turn's edits.</param>
    /// <returns>The text.</returns>
    public static string TurnDiff(IReadOnlyList<TranscriptEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var lines = new List<string>();
        foreach (var edit in edits)
        {
            lines.Add($"--- {edit.File}");
            if (edit.Before.Length > 0)
            {
                lines.AddRange(edit.Before.Split('\n').Select(line => $"- {line}"));
            }

            lines.AddRange(edit.After.Split('\n').Select(line => $"+ {line}"));
        }

        return string.Join('\n', lines);
    }

    // The share of the lines a turn added that no later edit of the same file removed; null when it added no line that counts.
    internal static double? Survival(IReadOnlyList<TranscriptTurn> session, int index)
    {
        var own = session[index].Edits;
        var added = 0;
        var alive = 0;
        for (var i = 0; i < own.Count; i++)
        {
            var file = own[i].File;
            var later = own.Skip(i + 1).Concat(session.Skip(index + 1).SelectMany(t => t.Edits)).Where(e => e.File == file).ToList();
            foreach (var line in own[i].After.Split('\n').Where(l => !TrivialLine.IsMatch(l)))
            {
                added++;
                if (!later.Exists(edit => Removes(edit, line)))
                {
                    alive++;
                }
            }
        }

        return added == 0 ? null : (double)alive / added;
    }

    private static bool Removes(TranscriptEdit edit, string line)
    {
        var inAfter = edit.After.Split('\n').Contains(line, StringComparer.Ordinal);
        return edit.Before.Length == 0 ? !inAfter : edit.Before.Split('\n').Contains(line, StringComparer.Ordinal) && !inAfter;
    }

    private static Reply Classify(string text)
    {
        if (text.Contains(Interrupt, StringComparison.Ordinal))
        {
            return Reply.Interrupt;
        }

        return CorrectionStart.IsMatch(text) ? Reply.Correction : BareContinueText.IsMatch(text) ? Reply.BareContinue : Reply.Other;
    }

    // Blank and brace-only lines carry no signal and survive trivially.
    [GeneratedRegex(@"^[\s{}()\[\];,]*$", RegexOptions.None, TimeoutMs)]
    private static partial Regex TrivialLine { get; }

    [GeneratedRegex(@"^\s*(no\b|nope|wait\b|stop\b|don'?t\b|actually\b|that'?s (wrong|not)|not what|undo|revert)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeoutMs)]
    private static partial Regex CorrectionStart { get; }

    [GeneratedRegex(@"^\s*(continue|keep going|go on|go ahead|proceed|yes|yep|y|ok|okay|do it|carry on)[\s.!]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeoutMs)]
    private static partial Regex BareContinueText { get; }
}
