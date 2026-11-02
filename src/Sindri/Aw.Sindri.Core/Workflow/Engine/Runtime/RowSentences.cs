using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The list row's own 'now' sentence (API spec §3.8 'now'), one pattern per chip: DECISION 'needs your &lt;answer&gt; (desk answer)',
/// PARKED and ESCALATED 'needs your answer' (or what the park waits for), WITH YOU 'with you: &lt;task&gt;', RUNNING
/// '&lt;actor&gt; &lt;verb&gt;ing in &lt;item&gt;', QUEUED 'queued', DONE (and FAILED, DIDN'T WORK OUT, CANCELLED) the outcome. It is not a cut of 'next'.
/// </summary>
internal static class RowSentences
{
    /// <summary>The sentence of <paramref name="view"/> under <paramref name="chip"/>, before the row cuts it to its limit.</summary>
    public static string Now(InstanceView view, StateChip chip, bool agentIdle = false) => chip.Word switch
    {
        StateChip.Queued => "queued",
        StateChip.Done or StateChip.Failed or StateChip.DidntWorkOut or StateChip.Cancelled => view.Outcome?.Value ?? "done",
        StateChip.WithYou => $"with you: {view.Activity ?? view.State}",
        StateChip.Decision => $"needs your {Answer(view)} (desk answer)",
        StateChip.Parked when IsUnavailable(view) => view.Question is { } question && Unavailables.IsIdleQuestion(question.Question) ? "agent stalled: idle, no report" : "waiting on a dependency that is down",
        StateChip.Parked or StateChip.Escalated => view.AnswerLine is not null ? "needs your answer" : $"{chip.Word.ToLowerInvariant()}{(view.Detail is { } detail ? $" ({detail})" : string.Empty)}",
        _ => Running(view, agentIdle),
    };

    /// <summary>Whether the park is a Step whose dependency stayed down: nobody is asked, the engine runs it again on a resume.</summary>
    internal static bool IsUnavailable(InstanceView view) =>
        view.EngineOwned is { Kind: EngineOwnedKind.Parked } parked && parked.Reason.StartsWith("unavailable", StringComparison.Ordinal);   // 'unavailable', or 'unavailable (child)' on a mirror

    /// <summary>What the open desk task asks for: its first output the report must carry, else the state's name.</summary>
    private static string Answer(InstanceView view) =>
        view.Task is { } task && task.Outputs.FirstOrDefault(o => DeskChecks.Applies(task, o)) is { } output ? SubjectRefs.Bare(output.Fact.Value) : view.Task?.State ?? view.State;

    private static string Running(InstanceView view, bool agentIdle)
    {
        var where = view.ItemId?.Value ?? view.Address.Value;
        return view.Step switch
        {
            { Kind: "active" } when agentIdle => $"agent stalled in {where}: idle, no report",
            { Kind: "active" } step => $"agent {Gerund(step.Ref)} in {where}",
            { Kind: "wait" } => $"engine waiting in {where}",
            _ => $"engine running in {where}",
        };
    }

    /// <summary>The -ing form of a block id's last word: 'step.produce' is 'producing', 'step.roll-back' is 'rolling back'.</summary>
    internal static string Gerund(string block)
    {
        var leaf = block[(block.LastIndexOf('.') + 1)..];
        var words = leaf.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "running";
        }

        words[0] = Ing(words[0]);
        return string.Join(' ', words);
    }

    private static string Ing(string verb) => verb switch
    {
        _ when verb.EndsWith("ie", StringComparison.Ordinal) => verb[..^2] + "ying",
        _ when verb.EndsWith('e') && !verb.EndsWith("ee", StringComparison.Ordinal) => verb[..^1] + "ing",
        _ when verb.Length >= 3 && IsDoubled(verb) => verb + verb[^1] + "ing",
        _ => verb + "ing",
    };

    /// <summary>A three-letter word of consonant, vowel, consonant ('run', 'get') doubles its last letter.</summary>
    private static bool IsDoubled(string verb) =>
        verb.Length == 3 && !IsVowel(verb[0]) && IsVowel(verb[1]) && !IsVowel(verb[2]) && verb[2] is not ('w' or 'x' or 'y');

    private static bool IsVowel(char c) => c is 'a' or 'e' or 'i' or 'o' or 'u';
}
