using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// One eval case being folded (spec §9.2): the instance after each line, and its history (each ledger event with the
/// instance after it). This is the eval's own test-harness state, threaded from line to line in memory; it is not a
/// store, and the interpreter keeps its state in Temporal (§5.1). Each line's trigger has a key of its own (its line
/// number is in the key). Pure and immutable: each line gives the next simulation, so the scaffold can try a line and
/// keep it only when it moves on. Every block has a runtime; a gate the line doesn't name passes; evidence holds.
/// </summary>
internal sealed record EvalSimulation(
    Recipe Recipe,
    BlockCatalog Catalog,
    IChildRecipeResolver Children,
    WorkflowInstance Instance,
    IReadOnlyList<InstanceChange> History)
{
    private const string Person = "human:eval";
    private const string Inbox = "service:workflow.inbox";
    private const string Timers = "service:workflow.timers";

    /// <summary>A step.failed line's reason when it gives none: the same on every line, so a repeat is 'sameFailure' (§3.7).</summary>
    internal const string DefaultFailure = "the Step failed";
    private static readonly FactKey _answer = FactKey.From("fact:answer");

    /// <summary>The case's instance, started with the line-1 inputs.</summary>
    public static Result<EvalSimulation> Start(EvalCase evalCase, Recipe recipe, BlockCatalog catalog, IChildRecipeResolver children)
    {
        ArgumentNullException.ThrowIfNull(evalCase);
        var id = WorkflowInstanceId.For(Guid(evalCase.Name + ":" + recipe.Id.Value));
        var pins = RecipeChildren.Of(recipe)
            .Select(c => (Id: c, Child: children.Resolve(c))).Where(static c => c.Child is not null)
            .ToDictionary(static c => c.Id, static c => c.Child!.Hash); // why: the Where keeps resolved children only
        var request = new StartRequest(
            TriggerKey.For(id, "start"),
            id,
            InstanceAddress.Root(ItemKey.From("EVAL-1"), recipe.Id, 1),
            new PinnedRecipe(recipe.Id, recipe.Version, recipe.Hash ?? RecipeHash.From("sha256:" + new string('0', 64)), pins),
            ItemKey.From("EVAL-1"),
            Attempt: 1,
            Parent: null,
            evalCase.Start.Start.Inputs.ToDictionary(static i => i.Key, static i => EvalFacts.Value(i.Value)),
            Person,
            TakenOver: false,
            SindriProfile.DefaultTenant,
            Ring: 0);
        return TransitionDecision.Start(request, recipe, Context(catalog, children)) switch
        {
            Applied applied => new EvalSimulation(recipe, catalog, children, applied.Changes[0].After, [applied.Changes[0]]),
            Rejected rejected => KernelErrors.Invalid.With($"the start was rejected: {rejected.Error.Code} {rejected.Error.Message}"),
            var other => KernelErrors.Invalid.With($"the start was {other.GetType().Name}"),
        };
    }

    /// <summary>Folds <paramref name="line"/>: builds its trigger, decides it, and checks nothing yet (see <see cref="Diverges"/>).</summary>
    /// <param name="line">The line.</param>
    /// <param name="number">Its line number in the file.</param>
    /// <returns>The next simulation and the decision, or what made the line unusable.</returns>
    public Result<(EvalSimulation Next, Decision Decision)> Step(EvalStepLine line, int number)
    {
        ArgumentNullException.ThrowIfNull(line);
        var built = Trigger(line.Trigger, number);
        if (built.IsError)
        {
            return built.Error;
        }

        var trigger = built.Value;
        var answers = Answers(trigger, line, number);
        if (answers.IsError)
        {
            return answers.Error;
        }

        var deciding = Decide(new DecisionInput(Instance, Recipe, trigger, answers.Value, Context(Catalog, Children)), line.Trigger.Kind);
        return deciding.IsError ? deciding.Error : Fold(deciding.Value, line.Trigger.Kind);
    }

    private static Result<Decision> Decide(DecisionInput input, string kind)
    {
        try
        {
            return TransitionDecision.Decide(input);
        }
        catch (NotSupportedException ex)
        {
            return KernelErrors.Invalid.With($"{kind} isn't decided by this build's engine yet ({ex.Message})");
        }
    }

    /// <summary>The decision's change to this instance, kept in the history; a rejection is a line the engine refused.</summary>
    private Result<(EvalSimulation Next, Decision Decision)> Fold(Decision decision, string kind)
    {
        var changes = decision switch
        {
            Applied applied => applied.Changes,
            Refused refused => refused.Changes,
            Rejected rejected => null,
            _ => [],
        };
        if (changes is null)
        {
            var error = ((Rejected)decision).Error;
            return KernelErrors.Invalid.With($"{kind} was rejected: {error.Code} {error.Message}");
        }

        var own = changes.FirstOrDefault(c => c.After.Id == Instance.Id);
        var next = this with
        {
            Instance = own?.After ?? Instance,
            History = own is null ? History : [.. History, own],
        };
        return (next, decision);
    }

    /// <summary>How the instance differs from <paramref name="expect"/>; null when it matches. Park and outcome are checked when the line gives them.</summary>
    public string? Diverges(EvalExpectation expect)
    {
        ArgumentNullException.ThrowIfNull(expect);
        var matches = string.Equals(expect.State, Instance.State, StringComparison.Ordinal)
            && (expect.Park is null || expect.Park == Instance.Park)
            && (expect.Outcome is null || expect.Outcome == Instance.Outcome);
        return matches
            ? null
            : $"expected {Describe(expect.State, expect.Park, expect.Outcome)}, got {Describe(Instance.State, Instance.Park, Instance.Outcome)}";
    }

    /// <summary>What the instance shows now, as an expectation.</summary>
    public EvalExpectation Shown => new(Instance.State, Instance.Park, Instance.Outcome);

    private static string Describe(string state, ParkReason? park, OutcomeName? outcome) =>
        state + (park is { } p ? $" ({Parks.Name(p)})" : string.Empty) + (outcome is { } o ? $" (outcome {o.Value})" : string.Empty);

    private Result<Trigger> Trigger(EvalTrigger line, int number)
    {
        if (EvalTriggerRules.Unfit(line) is { } unfit)
        {
            return KernelErrors.Invalid.With(unfit);
        }

        var stamp = new CommandStamp(CommandNonce.From(Hex("nonce:" + Text(number))[..32]), Hex("line:" + Text(number)));
        var outputs = (line.Outputs ?? new Dictionary<FactKey, JsonElement>()).ToDictionary(static o => o.Key, static o => EvalFacts.Value(o.Value));
        if (line.Kind == TriggerNames.StepCompleted && line.Outcome is { } reported)
        {
            var filled = EvalFilledOutputs.Add(outputs, Recipe, Catalog, (Instance.State, reported));
            if (filled.IsError)
            {
                return filled.Error;
            }

            outputs = filled.Value;
        }

        var report = Hex($"report:{Text(number)}:{line.Kind}:{line.Outcome?.Value}");
        return line.Kind switch
        {
            TriggerNames.StepCompleted when line.Outcome is { } outcome => Run(run => new StepCompleted(Person, run, outcome, outputs, Tokens: 0, StepRuntime.Agent, report)),
            TriggerNames.StepCompleted => Missing("step.completed needs the Step's 'outcome'"),
            TriggerNames.StepFailed => Run(run => new StepFailed(Person, run, line.Reason ?? DefaultFailure, Tokens: 0, report)),
            TriggerNames.WaitSatisfied => Satisfied(line, outputs, number),
            TriggerNames.ChildParked => Child(ChildChange.Parked, line.Park, number),
            TriggerNames.ChildResumed => Child(ChildChange.Resumed, reason: null, number),
            TriggerNames.ChildEscalated => Child(ChildChange.Escalated, reason: null, number),
            TriggerNames.Answer => new AnswerGiven(Person, stamp, EvalTriggerRules.Answer(line.Variant!.Value), AnswerText(line, number), line.Amount, Forwarded: null), // why: Unfit requires a variant with 'answer'
            TriggerNames.Takeover => new TakeoverRequested(Person, stamp),
            TriggerNames.Handback => new HandbackRequested(Person, stamp),
            TriggerNames.Cancel => new CancelRequested(Person, CancelOrigin.Command, stamp, Sequence: null, Parent: null),
            TriggerNames.Deadline => Deadline(),
            _ => new CommandFired(Person, CommandVerb.From(line.Kind[TriggerNames.CommandPrefix.Length..]), stamp),
        };
    }

    private static string? AnswerText(EvalTrigger line, int number) =>
        line.Outputs?.TryGetValue(_answer, out var text) == true
            ? EvalFacts.Text(text)
            : line.Variant == EvalVariant.Text ? $"answered in eval line {Text(number)}" : null;

    private Result<Trigger> Run(Func<RunToken, Trigger> report) => Instance.Frame.Run is { } run
        ? report(run.Token)
        : Missing($"'{Instance.State}' has no open Step run to report on");

    /// <summary>The wait of the recipe frame (also while a park mirrors its child) is satisfied by its own source.</summary>
    private Result<Trigger> Satisfied(EvalTrigger line, IReadOnlyDictionary<FactKey, FactValue> outputs, int number)
    {
        var frame = Instance.Frame;
        if (!Recipe.States.TryGetValue(frame.State, out var state) || state.WaitFor is not { } wait)
        {
            return Missing($"'{frame.State}' doesn't wait for anything");
        }

        if (EvalChildLine.Refusal(wait, line, outputs.Keys, Children) is { } refused)
        {
            return Missing(refused);
        }

        WaitSource? source = wait switch
        {
            { Event: { } kind } => new EventWaitSource(kind, number),
            { Resource: { } resource } => new ResourceWaitSource(resource, number),
            { Child: not null } when frame.Child is { } child => new ChildWaitSource(child, TriggerKey.For(child, "eval:" + Text(number))),
            _ => null,
        };
        return source is null
            ? Missing($"'{frame.State}' waits for an answer; answer it with kind 'answer' and 'variant': 'text' (its 'fact:answer' output is the answer's text)")
            : new WaitSatisfied(Inbox, source, line.Outcome, outputs);
    }

    private Result<Trigger> Child(ChildChange change, ParkReason? reason, int number)
    {
        var frame = Instance.Frame;
        if (frame.Child is not { } child)
        {
            return Missing($"'{frame.State}' has no child");
        }

        if (change == ChildChange.Parked && reason is null)
        {
            return Missing("child.parked needs the child's 'park' reason");
        }

        return new ChildChanged(Inbox, child, change, reason, $"the child {change.ToString().ToLowerInvariant()} (eval line {Text(number)})", TriggerKey.For(child, "eval:" + Text(number)));
    }

    /// <summary>
    /// The running timer fires (§9.2): the takeover's idle reminder while taken over; otherwise the state's timeout while
    /// nothing is suspended (a park or escalation stops it); once a 'then: retry' re-armed it, the retry timer.
    /// </summary>
    private Result<Trigger> Deadline() => Instance switch
    {
        { Ended: true } => Missing($"'{Instance.State}' has ended; no timer runs"),
        { Suspension.Kind: SuspensionKind.Takeover } => new DeadlineFired(Timers, Instance.Frame.Entry, DeadlineKind.TakeoverIdle),
        { Suspension: null } when Recipe.States.TryGetValue(Instance.Frame.State, out var state) && state.Timeout is not null =>
            new DeadlineFired(Timers, Instance.Frame.Entry, Instance.Frame.Retried ? DeadlineKind.Retry : DeadlineKind.State),
        _ => Missing($"'{Instance.State}' has no running timer"),
    };

    /// <summary>The matched transition's gates answer as the line says (pass when it doesn't name them); its evidence holds.</summary>
    private Result<GateAnswers> Answers(Trigger trigger, EvalStepLine line, int number)
    {
        var transition = Matched(trigger);
        var named = line.Gates ?? new Dictionary<BlockId, GateVerdict>();
        var stray = named.Keys.Where(g => transition is null || !transition.Gates.Any(u => u.Use == g)).Select(static g => g.Value).ToList();
        if (stray.Count > 0)
        {
            return Missing($"'gates' names {string.Join(", ", stray)}, which the matched transition doesn't run");
        }

        if (transition is null)
        {
            return GateAnswers.None;
        }

        var gates = transition.Gates.ToDictionary(
            static u => u.Use,
            u => named.TryGetValue(u.Use, out var verdict)
                ? new GateResult(verdict, verdict == GateVerdict.Pass ? 1.0 : 0.0, [$"eval line {Text(number)}: {verdict}"])
                : new GateResult(GateVerdict.Pass, 1.0, [$"eval line {Text(number)}: pass"]));
        var evidence = transition.Evidence.Distinct().ToDictionary(static c => c, c => new EvidenceResult(c.Expect == EvidenceExpectation.Pass, ["eval: holds"]));
        return new GateAnswers(gates, evidence);
    }

    private RecipeTransition? Matched(Trigger trigger) => MatchedTransition.Of(Instance, Recipe, trigger);

    private static DecisionContext Context(BlockCatalog catalog, IChildRecipeResolver children) =>
        new(ApprovalCheck.Holds, catalog, catalog.Blocks.ToDictionary(static b => b.Id, static _ => StepRuntime.Agent), children);

    private static Error Missing(string detail) => KernelErrors.Invalid.With(detail);

    private static string Hex(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static Guid Guid(string text) => new(SHA256.HashData(Encoding.UTF8.GetBytes("eval:" + text)).AsSpan(0, 16));

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}
