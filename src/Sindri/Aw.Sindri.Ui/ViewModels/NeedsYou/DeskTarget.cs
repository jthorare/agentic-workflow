using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>An open desk run as the instance serves it: where a report goes, and the command the step serves for completing it.</summary>
/// <param name="InstanceId">The instance's id.</param>
/// <param name="Run">The open run's token.</param>
/// <param name="Complete">The step's served complete command; null when none is served.</param>
/// <param name="Why">The desk state's served why; null or empty when the recipe serves none (or could not be read).</param>
/// <param name="IfApprove">What approving does, as the state's approve outcome says; null when not served.</param>
/// <param name="IfSendBack">What sending it back does, as the state's first revise outcome says; null when not served.</param>
/// <param name="Recommended">The outcome the step before the stop recommends, with its why; null when none is served.</param>
/// <param name="Questions">The ask step's questions, read from the escalation fact; null for every other desk.</param>
/// <param name="MergeCard">The pull request card, when the instance lists the fact "fact:prStatus" and it read; null otherwise.</param>
internal sealed record DeskTarget(string InstanceId, string Run, StatusCommand? Complete, string? Why = null, string? IfApprove = null, string? IfSendBack = null, DeskRecommendation? Recommended = null, MergeCardViewModel? MergeCard = null, IReadOnlyList<DeskQuestion>? Questions = null)
{
    /// <summary>The scoping interview's questions, read from "fact:questions"; null for every other desk.</summary>
    internal IReadOnlyList<ScopingQuestion>? Scoping { get; init; }

    /// <summary>True when the step's complete command is protected: a terminal completes it, so a send back is the step's "rework".</summary>
    internal bool IsTerminalOnly => Complete is { } command && string.Equals(command.Class, "protected", StringComparison.Ordinal);

    /// <summary>The keys of the outputs the report must carry (the served desk form's required outputs); empty when none.</summary>
    internal IReadOnlyList<string> RequiredOutputs { get; init; } = [];

    /// <summary>The instance's address, for taking the person to its frame; empty when not read.</summary>
    internal string Address { get; init; } = string.Empty;

    /// <summary>What the step asks the person to read, as served; empty when none.</summary>
    internal IReadOnlyList<DeskReview> Reviews { get; init; } = [];

    /// <summary>The ref (provider/id) of the agent session that runs the step, when the engine names it; null otherwise.</summary>
    internal string? SessionRef { get; init; }

    /// <summary>The code of why the step stands as it does; null when none is served.</summary>
    internal string? ReasonCode { get; init; }

    /// <summary>The recipe verbs the instance accepts now, in the order it serves them; empty when it serves none.</summary>
    internal IReadOnlyList<DeskVerb> Verbs { get; init; } = [];

    /// <summary>
    /// The outcome the app may report without asking the person anything: the one the step before the stop recommends, else the step's only outcome (from its served
    /// complete command). None when the outcome needs a reason or a confirm (revise, rework, drop), when the step asks questions, shows a pull request or is terminal-only,
    /// or when it has several outcomes and none is recommended.
    /// </summary>
    /// <returns>The outcome, or null when the person has to choose.</returns>
    internal string? InputFreeOutcome()
    {
        if (IsTerminalOnly || MergeCard is not null || Questions is { Count: > 0 } || Scoping is { Count: > 0 })
        {
            return null;
        }

        var outcome = Recommended is { Outcome.Length: > 0 } recommended ? recommended.Outcome : SoleOutcome(Complete?.Cli ?? "");
        return outcome is not null && !NeedsInput(outcome) ? outcome : null;
    }

    // Revising or reworking asks for a reason and dropping asks to be confirmed: the action bar does both.
    private static bool NeedsInput(string outcome) =>
        outcome.StartsWith("revise", StringComparison.OrdinalIgnoreCase) || outcome.StartsWith("rework", StringComparison.OrdinalIgnoreCase) || outcome.StartsWith("drop", StringComparison.OrdinalIgnoreCase);

    // The served line names one outcome ("--outcome done"), several ("--outcome <a|b>") or none ("--outcome <outcome>").
    private static string? SoleOutcome(string cli)
    {
        const string flag = "--outcome ";
        var at = cli.IndexOf(flag, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        var rest = cli.AsSpan(at + flag.Length);
        var end = rest.IndexOf(' ');
        var word = (end < 0 ? rest : rest[..end]).ToString();
        return word.Length > 0 && word[0] != '<' ? word : null;
    }
}

