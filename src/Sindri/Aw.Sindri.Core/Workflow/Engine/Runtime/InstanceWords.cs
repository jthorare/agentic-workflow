using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The words status prints for one instance (engine spec §3.8, §6.4): the header's detail, 'next', the answer line and
/// the drift line. Every text is plain ASCII.
/// </summary>
/// <param name="Instance">The interpreter's state.</param>
/// <param name="Recipe">The recipe it runs.</param>
/// <param name="Since">When it entered its state.</param>
/// <param name="Child">The child a park or escalation mirrors, when it has one.</param>
internal sealed partial record InstanceWords(WorkflowInstance Instance, InterpretedRecipe Recipe, DateTimeOffset Since, ChildView? Child)
{
    /// <summary>Between an answer line and its note, as §6.4 spaces it.</summary>
    private const string Gap = "     ";

    private string Address => Instance.Address.Value;

    /// <summary>The recipe as the CLI names it: 'id@hash12'.</summary>
    private string Pinned => $"{Recipe.Version.Id.Value}@{StatusText.Hash12(Recipe.Version.Hash.Value)}";

    /// <summary>What follows the state in the header: the park reason, the escalation kind, or the outcome when it isn't the state's name.</summary>
    /// <returns>The detail, or null.</returns>
    public string? Detail() => Instance.Suspension switch
    {
        _ when Instance.Ended => Instance.Outcome?.Value is { } outcome && outcome != Instance.State ? outcome : null,
        { Kind: SuspensionKind.Park } park => ParkDetail(park),
        { Kind: SuspensionKind.Escalation, Escalation: { } kind } => Escalation(kind),
        _ => null,
    };

    /// <summary>The answer line §6.4 shows for the open park or escalation; null when none applies.</summary>
    /// <returns>The line.</returns>
    public string? Answer() => Instance.Suspension switch
    {
        _ when Instance.Ended => null,
        { Kind: SuspensionKind.Park, Mirrors: not null } park when !AnswerVariants.ReasonFixesChildFit(park.Reason) => $"{Answering} <the child's answer>{Gap}(forwarded to {ChildName})",
        { Kind: SuspensionKind.Park, Reason: ParkReason.SpecConflict } park => SpecConflictAnswer(park),
        { Kind: SuspensionKind.Park } park => ParkAnswer(park),
        { Kind: SuspensionKind.Escalation, Escalation: EscalationKind.Gate } => $"{Answering} --accept{Gap}(at a terminal)",
        { Kind: SuspensionKind.Escalation, Escalation: EscalationKind.Timeout } => $"{Answering} --resume",
        { Kind: SuspensionKind.Escalation } => $"{Answering} <the child's answer>{Gap}(forwarded to {ChildName})",
        _ => null,
    };

    /// <summary>A drift recorded behind the open park (§6.4): the block, and that the answer needs the re-approval first.</summary>
    /// <returns>The line, or null.</returns>
    public string? Drift() => BehindDrift() is { } drift
        ? $"{Block(drift.Question)} changed since {Pinned} was approved; answer after the re-approval: sindri recipe approve {Pinned}"
        : null;

    /// <summary>The drift park waiting behind the open park that the re-approval hasn't ended; the line and the note both read it.</summary>
    private Suspension? BehindDrift() => Instance is { Suspension.Kind: SuspensionKind.Park }
        ? Instance.PendingParks().FirstOrDefault(static p => p is { Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Drift, Approved: false })
        : null;

    private string Answering => $"sindri workflow answer {Address}";

    private string ChildName => Child?.Address.Value ?? "the child";

    private static string? ParkDetail(Suspension park)
    {
        if (park.Reason is not { } reason)
        {
            return null;
        }

        if (reason == ParkReason.SpecConflict)
        {
            return $"spec-conflict ({Name(park.ParkKind)})";
        }

        return park.Mirrors is null ? Name(reason) : $"{Name(reason)} (child)";
    }

    private string ParkAnswer(Suspension park)
    {
        var forwarded = park.Mirrors is null ? string.Empty : "; forwarded to the child";
        return park.Reason switch
        {
            ParkReason.Rounds or ParkReason.NoProgress => $"{Answering} --grant 1{Gap}(at a terminal{forwarded})",
            ParkReason.Budget => $"{Answering} --grant-tokens {GrantTokens()}{Gap}(at a terminal; once per park)",
            ParkReason.NeedsAnswer when park.ParkKind == ParkKind.Wait => $"{Answering} --resume [--text <answer>]",
            ParkReason.NeedsAnswer => $"{Answering} --text <answer>",
            ParkReason.Timeout or ParkReason.SpecConflict or ParkReason.NoRuntime or ParkReason.Launch or null or _ => $"{Answering} --resume",
        };
    }

    private string SpecConflictAnswer(Suspension park) => park.ParkKind switch
    {
        ParkKind.Revoked => $"none (revoked): sindri workflow cancel {Address}, then start a new attempt with --attempt next",
        ParkKind.ChildRevoked => $"none (child revoked): sindri recipe approve {Recipe.Version.Id.Value}{Gap}(at a terminal; the approval resumes it)",
        ParkKind.Drift => $"none (drift): sindri recipe approve {Pinned}{Gap}(at a terminal; the approval resumes it)",
        ParkKind.Dispatch => $"{Answering} --resume{Gap}(dispatch: once {(park.Question.Contains(DispatchWords.CantRun, StringComparison.Ordinal) ? "the cause is fixed" : "the profile allows the effect")})",
        ParkKind.Loop or ParkKind.Refusal or ParkKind.Step or ParkKind.Wait or ParkKind.Unconfinable or ParkKind.SelfCheck or null or _ => $"{Answering} --resume",
    };

    /// <summary>The grant a budget park suggests: half the active state's budget, else half the recipe's.</summary>
    internal string GrantTokens()
    {
        var cap = Recipe.Recipe.States.TryGetValue(Instance.Frame.State, out var state) && state.Budget is { } own
            ? own.Tokens
            : Recipe.Recipe.Budget?.Tokens;
        return cap is { } tokens ? TokenCounts.Format(Math.Max(1, tokens / 2)) : "<n>";
    }

    /// <summary>The block a drift or no-runtime park names: its question when that is a block id, otherwise words for it.</summary>
    private static string Block(string question) =>
        BlockId.TryFrom(question.TrimEnd('.'), out var block) ? block.Value : "A pinned block";

    private static string Escalation(EscalationKind kind) => kind switch
    {
        EscalationKind.Gate => "gate",
        EscalationKind.Timeout => "timeout",
        EscalationKind.Child or _ => "child",
    };

    private static string Name(ParkReason reason) => reason switch
    {
        ParkReason.Rounds => "rounds",
        ParkReason.NoProgress => "no-progress",
        ParkReason.Budget => "budget",
        ParkReason.NeedsAnswer => "needs-answer",
        ParkReason.Timeout => "timeout",
        ParkReason.SpecConflict => "spec-conflict",
        ParkReason.Launch => "launch",
        ParkReason.NoRuntime or _ => "SND-WF-010",
    };

    private static string Name(ParkKind? kind) => kind switch
    {
        ParkKind.Revoked => "revoked",
        ParkKind.ChildRevoked => "child revoked",
        ParkKind.Drift => "drift",
        ParkKind.Dispatch => "dispatch",
        ParkKind.Unconfinable => "unconfinable",
        ParkKind.SelfCheck => "self-check",
        ParkKind.Loop or ParkKind.Refusal or ParkKind.Step or ParkKind.Wait or null or _ => "unknown",
    };
}
