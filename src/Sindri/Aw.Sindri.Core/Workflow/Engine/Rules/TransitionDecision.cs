using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The engine's one decision function (spec §3.5, §5.1, §5.2, §5.8, W13, W14): given the instance, its recipe, a
/// trigger and the matched transition's gate answers, the instance after it, its ledger events, its effects and its
/// follow-ups. Pure: everything it reads is in its input, so the recipe interpreter calls it from workflow code. It
/// applies the validation it can see (run token and state, a malformed report, approval), matches the transition, and
/// hands refusals to <see cref="RefusalRules"/>, suspensions to <see cref="Suspensions"/>, loops and budgets to
/// <see cref="LoopRules"/> and <see cref="BudgetRules"/>, children to <see cref="ChildPropagation"/>, and builds each
/// event's post-state delta with <see cref="InstanceDeltas.Between"/>.
/// </summary>
public static partial class TransitionDecision
{
    /// <summary>
    /// A new instance (spec §5.1): the instance in the recipe's initial state as entry 1, with its run opened (an active
    /// state) or its child created (a child wait), and its workflow.started event.
    /// </summary>
    /// <param name="request">The new instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="context">The approval, catalog, runtimes and children.</param>
    /// <returns><see cref="Applied"/> with the started instance (and a created child's), or <see cref="Rejected"/>.</returns>
    public static Decision Start(StartRequest request, Recipe recipe, DecisionContext context) =>
        Start(request, recipe, context, EngineRules.Default);

    /// <summary>
    /// The transition <paramref name="trigger"/> selects from the current state (spec §3.5): the first in file order
    /// whose 'on' is the trigger and whose 'when' holds. A report for another run is <see cref="MatchMiss.StaleRun"/>;
    /// an engine trigger is <see cref="MatchMiss.EngineTrigger"/>, except a text answer on an answer wait, which matches
    /// as the 'wait.satisfied' it stands for (§3.4).
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="trigger">The trigger.</param>
    /// <returns>The match, so the caller evaluates exactly its gates and evidence.</returns>
    public static TransitionMatch Match(WorkflowInstance instance, Recipe recipe, Trigger trigger)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(trigger);
        return MatchFrom(Unmirrored(instance, trigger), recipe, AnswerSatisfies(instance, recipe, trigger) ?? trigger);
    }

    /// <summary>Decides one trigger (spec §5.2, §5.8).</summary>
    /// <param name="input">The instance, recipe, trigger, gate answers and context.</param>
    /// <returns>Applied, refused, absorbed or rejected.</returns>
    public static Decision Decide(DecisionInput input) => Decide(input, EngineRules.Default);

    /// <summary><see cref="Decide(DecisionInput)"/> through <paramref name="rules"/>.</summary>
    internal static Decision Decide(DecisionInput input, IEngineRules rules)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(rules);
        var deciding = new Deciding(input, rules, input.Trigger.KeyFor(input.Instance.Id));
        return input.Trigger is StepCompleted or StepFailed or WaitSatisfied or CommandFired
            ? DecideRecipeTrigger(deciding)
            : DecideEngineTrigger(deciding);
    }

    /// <summary>
    /// A recipe trigger (§3.5, §5.2): validate, check the approval, spend, then match and apply or refuse.
    /// <paramref name="from"/> is the instance to decide from when it differs from the input's (an answer wait's 'fact:answer').
    /// </summary>
    private static Decision DecideRecipeTrigger(Deciding d, WorkflowInstance? from = null)
    {
        if (from is not null)
        {
            return DecideRecipeTrigger(d, from, released: null);
        }

        var freed = Release(d);
        return DecideRecipeTrigger(d, freed.Instance, freed.Released);
    }

    private static Decision DecideRecipeTrigger(Deciding d, WorkflowInstance instance, IReadOnlyList<RefusedEffect>? released) =>
        WithReleased(DecideFrom(d, instance), released);

    private static Decision DecideFrom(Deciding d, WorkflowInstance instance)
    {
        var match = MatchFrom(instance, d.Recipe, d.Trigger);
        if (Invalid(d, instance, match) is { } invalid)
        {
            return new Rejected(d.Key, invalid);
        }

        if (Unapproved(d, instance, match.Transition) is { } unapproved)
        {
            return unapproved;
        }

        var spend = Spend(d, instance);
        if (spend.Park is { } budget && !KeepsReport(d))
        {
            return d.Changed(d.Rules.Suspend(instance with { Spent = spend.Spent }, Caused(budget, d.Trigger, match.Transition)));
        }

        var spent = instance with { Spent = spend.Spent };
        return match.Transition is { } index ? Transition(d, spent, index) : Unmatched(d, spent);
    }

    /// <summary>
    /// The park that stands in for any new run of the Step when its budget is already spent (a kept report spent it): null while the Step may start, or in a history from
    /// before the fix. Its answer starts the Step without counting a round.
    /// </summary>
    private static Suspension? StartBlocked(Deciding d, WorkflowInstance instance) =>
        !d.Context.LegacyBudgetPark && BudgetRules.Exhausted(instance, d.Recipe) is { } question ? BudgetRules.StartPark(question, d.Trigger.Actor) : null;

    /// <summary>An accepted report is kept whatever it cost: its transition applies and the budget is checked where the next Step would start (BudgetRules.StartPark).</summary>
    private static bool KeepsReport(Deciding d) => d.Trigger is StepCompleted && !d.Context.LegacyBudgetPark;

    /// <summary>
    /// A park a rule decided carries no actor and no withheld trigger (LoopRules, BudgetRules); the decision that took the
    /// trigger sets both, so the park shows who caused it and its resume can perform or re-run what it withheld.
    /// </summary>
    private static Suspension Caused(Suspension park, Trigger trigger, int? transition) => park with
    {
        Actor = park.Actor.Length == 0 ? trigger.Actor : park.Actor,
        Resume = park.Resume.Withheld is null ? park.Resume with { Withheld = new WithheldTrigger(transition, trigger) } : park.Resume,
    };

    /// <summary>A child's end first ends the parent's suspension that mirrors that child (§5.8), so a child that ends releases its parent.</summary>
    private static ParentReleased Release(Deciding d) =>
        d.Trigger is WaitSatisfied { Source: ChildWaitSource child } && Mirrors(d.Input.Instance, child.Child)
            ? d.Rules.Release(d.Input.Instance, child.Child)
            : new ParentReleased(d.Input.Instance);

    private static bool Mirrors(WorkflowInstance instance, WorkflowInstanceId child) =>
        instance.Suspension?.Mirrors == child || Suspensions.PendingParks(instance).Any(p => p.Mirrors == child);

    /// <summary>Allowed, but no 'when' holds (§3.5): a step.failed parks with needs-answer; any other trigger is SND-WF-003.</summary>
    private static Decision Unmatched(Deciding d, WorkflowInstance instance)
    {
        if (d.Trigger is not StepFailed failed)
        {
            return new Rejected(d.Key, WorkflowErrors.TriggerNotAllowed.With(
                $"no transition from '{instance.State}' on {d.Trigger.Name} holds for this {Describe(d.Trigger)} (fix: see allowedCommands in status)"));
        }

        var step = d.Recipe.States[instance.State].Step?.Use.Value ?? instance.State;
        if (!d.Context.LegacyUnavailable)
        {
            return Unavailable(d, instance, failed, step);
        }

        var park = new Suspension(
            SuspensionKind.Park, ParkReason.NeedsAnswer, ParkKind.Step, Escalation: null, failed.Detail is { } detail ? $"{step} failed: {failed.Reason}: {detail}" : $"{step} failed: {failed.Reason}", failed.Actor, Mirrors: null,
            new ResumeAction(ResumeKind.Rerun, Withheld: null, Gate: null, Effects: []));
        return d.Changed(d.Rules.Suspend(instance, park));
    }

    /// <summary>
    /// A Step failure no transition takes (owner decision q90, q93): the dependency is down, which no human can answer. The Step runs again, a new run in the same
    /// entry, until <see cref="Unavailables.MaxRetries"/> reruns failed; then it parks as unavailable, whose answer is a rerun. A retry counts no loop round.
    /// </summary>
    private static Applied Unavailable(Deciding d, WorkflowInstance instance, StepFailed failed, string step)
    {
        var dependency = failed.Dependency ?? step;
        var error = failed.Detail is { } detail ? $"{failed.Reason}: {detail}" : failed.Reason;
        if (instance.Frame.Run is not { } run || d.Recipe.States[instance.State].Step is not { } use || run.Retries >= Unavailables.MaxRetries)
        {
            var retries = instance.Frame.Run?.Retries ?? 0;
            var question = Unavailables.IsGoneSession(failed) ? Unavailables.Gone(dependency, failed.Reason, retries, error) : Unavailables.Question(dependency, retries, error);
            var park = Unavailables.Park(question, failed.Actor, failed.Code);
            return d.Changed(d.Rules.Suspend(instance, park));
        }

        var next = run with { Token = RunToken.For(run.Token.Entry, run.Token.Number + 1), Launch = null, Retries = run.Retries + 1 };
        var applied = d.Changed(instance with { Frame = instance.Frame with { Run = next } });
        return applied with { FollowUps = [new FollowUp(FollowUpKind.StartRun, instance.Id, next.Token, use.Use, Answer: null)] };
    }

    /// <summary>One trigger being decided, with the rules it hands work to.</summary>
    /// <param name="Input">What the decision reads.</param>
    /// <param name="Rules">The entry, suspension, loop, budget, child and delta rules.</param>
    /// <param name="Key">The trigger's key.</param>
    private sealed partial record Deciding(DecisionInput Input, IEngineRules Rules, TriggerKey Key)
    {
        public Recipe Recipe => Input.Recipe;

        public Trigger Trigger => Input.Trigger;

        public DecisionContext Context => Input.Context;

        /// <summary>The scope a state entry runs in.</summary>
        public Scope Scope => new(Rules, Context, Recipe, Trigger.Actor);
    }

    /// <summary>What entering a state reads: the rules, the context, the recipe, and the actor a created child keeps.</summary>
    /// <param name="Rules">The rules.</param>
    /// <param name="Context">The context.</param>
    /// <param name="Recipe">The recipe.</param>
    /// <param name="Actor">The trigger's actor.</param>
    private sealed record Scope(IEngineRules Rules, DecisionContext Context, Recipe Recipe, string Actor);
}
