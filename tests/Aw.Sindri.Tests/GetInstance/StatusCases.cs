using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Tests.Workflow.Engine;
using RecipeDocument = Aw.Sindri.Core.Workflow.Recipes.Recipe;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>
/// One instance per §6.4 layout and per §3.8 row P4 can reach, over the shipped workflow.step, as 'GetInstance' builds
/// its view (<see cref="InstanceViews.Of(WorkflowInstance, InterpretedRecipe, DateTimeOffset, DateTimeOffset?, InstanceViewContext)"/>).
/// </summary>
internal static class StatusCases
{
    /// <summary>The time every case is rendered at.</summary>
    public static DateTimeOffset Now => EngineFixture.Now;

    public static IReadOnlyList<string> Names { get; } =
    [
        "desk-task", "parked-no-progress-child", "parked-budget", "escalated-gate", "taken-over", "done",
        "parked-rounds", "parked-needs-answer-step", "parked-needs-answer-wait", "parked-timeout", "parked-revoked",
        "parked-drift", "parked-no-runtime", "parked-rounds-with-drift", "cancelled", "escalated-child", "parked-mirrored-needs-answer",
        "parked-budget-odd", "parked-budget-no-cap", "parked-mirrored-spec-conflict", "parked-mirrored-needs-answer-wait", "parked-mirrored-rounds", "parked-mirrored-timeout", "parked-mirrored-no-runtime", "parked-mirrored-budget", "parked-mirrored-two-children", "parked-mirrored-unlisted-child",
    ];

    /// <summary>The cases whose suspension mirrors a child.</summary>
    public static IReadOnlyList<string> Mirrored { get; } =
    [
        "parked-no-progress-child", "escalated-child", "parked-mirrored-needs-answer", "parked-mirrored-spec-conflict", "parked-mirrored-needs-answer-wait", "parked-mirrored-rounds", "parked-mirrored-timeout", "parked-mirrored-no-runtime", "parked-mirrored-budget", "parked-mirrored-two-children", "parked-mirrored-unlisted-child",
    ];

    /// <summary>The child every mirror in these cases names, and the id a suspension mirroring it carries.</summary>
    private static readonly InstanceAddress _childAddress = InstanceAddress.From("T-3/step/1/step.4");

    private static WorkflowInstanceId MirroredId { get; } = WorkflowInstanceId.Of(_childAddress);

    public static InterpretedRecipe Recipe { get; } = new(
        new RecipeVersion(EngineFixture.Step.Id, EngineFixture.Step.Version, EngineFixture.Step.Hash!.Value), // why: the shipped recipe is read with its hash
        EngineFixture.Step,
        EngineFixture.Catalog,
        new Dictionary<FactKey, InputFact> { [FactKey.From("fact:spec")] = new("spec.md", EngineFixture.Fact("the spec")) });

    public static InstanceView View(string name)
    {
        var built = Build(name);
        return InstanceViews.Of(built.Instance, built.Recipe ?? Recipe, built.Since, built.TimeoutAt, new InstanceViewContext(Version: 4, Children: built.Children));
    }

    /// <summary>The interpreter state a case's view is built from.</summary>
    /// <param name="name">The case.</param>
    /// <returns>The instance.</returns>
    public static WorkflowInstance Instance(string name) => Build(name).Instance;

    /// <summary>The recipe a case runs: the shipped workflow.step, or it with the case's own budget.</summary>
    /// <param name="name">The case.</param>
    /// <returns>The recipe.</returns>
    public static RecipeDocument RecipeOf(string name) => (Build(name).Recipe ?? Recipe).Recipe;

    private sealed record Built(WorkflowInstance Instance, DateTimeOffset Since, DateTimeOffset? TimeoutAt, IReadOnlyList<ChildView>? Children, InterpretedRecipe? Recipe = null);

    private static Built Build(string name) => name switch
    {
        "desk-task" => Case(Produce(), Ago(72), timeoutAt: Now + new TimeSpan(days: 1, hours: 22, minutes: 48, seconds: 0)),
        "parked-no-progress-child" => Case(MirroringChild(), Ago(12), children: [new ChildView(InstanceAddress.From("T-3/step/1/step.4"), "parked: no-progress")]),
        "parked-budget" => Case(Park(ParkReason.Budget, ParkKind.Step, "produce used 5M of 5M tokens") with { Spent = Spent(5_000_000) }, Ago(3)),
        "escalated-gate" => Case(Escalated(), Ago(120)),
        "taken-over" => Case(TakenOver(TimeSpan.FromSeconds(40)), Ago(5)),
        "done" => Case(Ended("done", "resolved"), Ago(1)),
        "parked-rounds" => Case(Park(ParkReason.Rounds, ParkKind.Loop, "revisions used 3 of 3 rounds"), Ago(30)),
        "parked-needs-answer-step" => Case(Park(ParkReason.NeedsAnswer, ParkKind.Step, "step.produce failed: the spec is ambiguous"), Ago(8)),
        "parked-needs-answer-wait" => Case(Park(ParkReason.NeedsAnswer, ParkKind.Wait, "gate.test.check refused produce -> checks (no output)"), Ago(8)),
        "parked-timeout" => Case(Park(ParkReason.Timeout, kind: null, "produce ran past 2d"), Ago(60 * 26)),
        "parked-revoked" => Case(Park(ParkReason.SpecConflict, ParkKind.Revoked, "workflow.step was revoked."), Ago(4)),
        "parked-dispatch-no-runtime" => Case(DispatchPark("effect.claim.ref can't run: repo 'x' isn't in the approved profile (SND-WF-010). Fix that, then resume to send it again."), Ago(4)),
        "parked-dispatch-effect-failed" => Case(DispatchPark("effect.claim.ref can't run: the ref could not be created. Fix that, then resume to send it again."), Ago(4)),
        "parked-dispatch-allowlist" => Case(DispatchPark("effect.claim.ref isn't on the profile's effect allowlist. Add it and approve the profile, then resume to send it again."), Ago(4)),
        "parked-drift" => Case(Park(ParkReason.SpecConflict, ParkKind.Drift, "step.produce"), Ago(4)),
        "parked-no-runtime" => Case(Park(ParkReason.NoRuntime, kind: null, "step.test.agent"), Ago(2)),
        "parked-rounds-with-drift" => Case(Park(ParkReason.Rounds, ParkKind.Loop, "revisions used 3 of 3 rounds") with { PendingPark = Suspended(ParkReason.SpecConflict, ParkKind.Drift, "step.produce") }, Ago(30)),
        "cancelled" => Case(Ended("cancelled", "cancelled"), Ago(1)),
        "failed" => Case(Ended("failed", "failed"), Ago(1)),
        "unresolved" => Case(Ended("unresolved", "unresolved"), Ago(1)),
        "parked-rounds-with-approved-drift" => Case(Park(ParkReason.Rounds, ParkKind.Loop, "revisions used 3 of 3 rounds") with { PendingPark = Suspended(ParkReason.SpecConflict, ParkKind.Drift, "step.produce") with { Approved = true } }, Ago(30)),
        "parked-rounds-with-later-drift" => Case(Park(ParkReason.Rounds, ParkKind.Loop, "revisions used 3 of 3 rounds") with
        {
            PendingPark = Suspended(ParkReason.Timeout, kind: null, "produce waited too long"),
            LaterParks = [Suspended(ParkReason.SpecConflict, ParkKind.Drift, "step.produce")],
        }, Ago(30)),
        _ => BuildMirrored(name),
    };

    private static Built BuildMirrored(string name) => name switch
    {
        "escalated-child" => Case(EscalatedChild(), Ago(15), children: [new ChildView(InstanceAddress.From("T-3/step/1/step.4"), "escalated: gate")]),
        "parked-mirrored-needs-answer" => Case(Produce() with
        {
            Suspension = Suspended(ParkReason.NeedsAnswer, kind: null, "the child's step failed") with { Mirrors = MirroredId },
        }, Ago(9), children: [new ChildView(InstanceAddress.From("T-3/step/1/step.4"), "parked: needs-answer")]),
        "parked-budget-odd" => Case(Park(ParkReason.Budget, ParkKind.Step, "produce used 25k of 25k tokens") with { Spent = Spent(25_000) }, Ago(3), recipe: WithProduceBudget(25_000)),
        "parked-budget-no-cap" => Case(Park(ParkReason.Budget, ParkKind.Refusal, "gate.budget.available refused produce -> checks (no budget left)"), Ago(3), recipe: WithoutBudgets()),
        "parked-mirrored-spec-conflict" => Case(MirroredPark(ParkReason.SpecConflict, "the child's recipe changed"), Ago(10), children: [Child("parked: spec-conflict")]),
        "parked-mirrored-needs-answer-wait" => Case(MirroredWait(), Ago(9), children: [Child("parked: needs-answer")]),
        "parked-mirrored-two-children" => Case(MirroredPark(ParkReason.Budget, "the child used 50k of 50k tokens"), Ago(10), children: [new ChildView(InstanceAddress.From("T-3/step/1/step.2"), "done"), Child("parked: budget")]),
        "parked-mirrored-unlisted-child" => Case(MirroredPark(ParkReason.Budget, "the child used 50k of 50k tokens"), Ago(10), children: [new ChildView(InstanceAddress.From("T-3/step/1/step.2"), "done")]),
        "parked-mirrored-rounds" => Case(MirroredPark(ParkReason.Rounds, "the child used all 3 rounds"), Ago(10), children: [Child("parked: rounds")]),
        "parked-mirrored-timeout" => Case(MirroredPark(ParkReason.Timeout, "the child ran past 2d"), Ago(10), children: [Child("parked: timeout")]),
        "parked-mirrored-no-runtime" => Case(MirroredPark(ParkReason.NoRuntime, "no runtime for step.test.agent"), Ago(10), children: [Child("parked: no-runtime")]),
        "parked-mirrored-budget" => Case(MirroredPark(ParkReason.Budget, "the child used 50k of 50k tokens"), Ago(10), children: [Child("parked: budget")]),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "no such status case"),
    };

    private static Built Case(WorkflowInstance instance, DateTimeOffset since, DateTimeOffset? timeoutAt = null, IReadOnlyList<ChildView>? children = null, InterpretedRecipe? recipe = null) => new(instance, since, timeoutAt, children, recipe);

    private static ChildView Child(string state) => new(InstanceAddress.From("T-3/step/1/step.4"), state);

    /// <summary>A park mirroring the child, as the engine builds it (§5.8): the child's reason, no park kind of its own (the child's is MirroredParkKind).</summary>
    private static WorkflowInstance MirroredPark(ParkReason reason, string question) => Produce() with
    {
        Suspension = Suspended(reason, kind: null, question) with { Mirrors = MirroredId, Resume = Resume(ResumeKind.Forward) },
    };

    private static WorkflowInstance MirroredWait()
    {
        var park = MirroredPark(ParkReason.NeedsAnswer, "the child's wait could not read the reply");
        return park with { Suspension = park.Suspension! with { MirroredParkKind = ParkKind.Wait } };
    }

    /// <summary>The shipped recipe with no budget on the recipe or any state: a gate.budget refusal has no cap to halve.</summary>
    private static InterpretedRecipe WithoutBudgets() => Recipe with
    {
        Recipe = Recipe.Recipe with
        {
            Budget = null,
            States = Recipe.Recipe.States.ToDictionary(static s => s.Key, static s => s.Value with { Budget = null }, StringComparer.Ordinal),
        },
    };

    /// <summary>The shipped recipe with the produce state's budget set to <paramref name="tokens"/>.</summary>
    private static InterpretedRecipe WithProduceBudget(int tokens) => Recipe with
    {
        Recipe = Recipe.Recipe with
        {
            States = Recipe.Recipe.States.ToDictionary(static s => s.Key, s => s.Key == "produce" ? s.Value with { Budget = new BudgetSpec(tokens) } : s.Value, StringComparer.Ordinal),
        },
    };

    private static DateTimeOffset Ago(int minutes) => Now - TimeSpan.FromMinutes(minutes);

    private static WorkflowInstance Produce() => EngineFixture.At(EngineFixture.Step, "produce");

    private static WorkflowInstance Park(ParkReason reason, ParkKind? kind, string question) =>
        Produce() with { Suspension = Suspended(reason, kind, question) };

    private static WorkflowInstance DispatchPark(string question) =>
        Produce() with { Suspension = Suspended(ParkReason.SpecConflict, ParkKind.Dispatch, question) };

    private static Suspension Suspended(ParkReason reason, ParkKind? kind, string question) =>
        new(SuspensionKind.Park, reason, kind, Escalation: null, question, "service:workflow", Mirrors: null, Resume(ResumeKind.Rerun));

    private static ResumeAction Resume(ResumeKind kind) => new(kind, Withheld: null, Gate: null, Effects: []);

    /// <summary>A no-progress park mirroring its child (§5.8).</summary>
    private static WorkflowInstance MirroringChild() => Produce() with
    {
        Suspension = Suspended(ParkReason.NoProgress, ParkKind.Loop, "checks failed twice with the same findings") with { Mirrors = MirroredId },
    };

    /// <summary>An escalation mirroring a child (§5.8): the child's escalation, forwarded; its own variants are the child's.</summary>
    private static WorkflowInstance EscalatedChild() => Produce() with
    {
        Suspension = new Suspension(
            SuspensionKind.Escalation, Reason: null, ParkKind: null, EscalationKind.Child, "the child's gate escalated", "service:workflow", Mirrors: MirroredId, Resume(ResumeKind.Forward)),
    };

    private static WorkflowInstance TakenOver(TimeSpan? paused = null) => Produce() with
    {
        Suspension = new Suspension(SuspensionKind.Takeover, Reason: null, ParkKind: null, Escalation: null, string.Empty, "human:thor", Mirrors: null, Resume(ResumeKind.Return)) { Remaining = paused is { } left ? new RemainingTimer(DeadlineKind.State, left) : null },
    };

    /// <summary>gate.verify.judge escalated on verify -> done (transition 3 of workflow.step).</summary>
    private static WorkflowInstance Escalated()
    {
        var report = EngineFixture.Completed(3, "accepted");
        return EngineFixture.At(EngineFixture.Step, "verify", entry: 3) with
        {
            Suspension = new Suspension(
                SuspensionKind.Escalation, Reason: null, ParkKind: null, EscalationKind.Gate, "gate.verify.judge scored 0.62, below 0.8", "service:workflow", Mirrors: null,
                new ResumeAction(ResumeKind.Override, new WithheldTrigger(Transition: 3, report), BlockId.From("gate.verify.judge"), Effects: [])),
        };
    }

    private static WorkflowInstance Ended(string state, string outcome) =>
        EngineFixture.At(EngineFixture.Step, "done", entry: 7) with { Frame = new Frame(state, Entry: 7, Run: null, Child: null), Outcome = OutcomeName.From(outcome) };

    private static TokenSpend Spent(int tokens) =>
        new(tokens, new Dictionary<string, int>(StringComparer.Ordinal) { ["produce"] = tokens }, Granted: 0);
}
