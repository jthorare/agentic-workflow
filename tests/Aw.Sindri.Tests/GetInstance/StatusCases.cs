using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Tests.Workflow.Engine;

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
        "parked-drift", "parked-no-runtime", "parked-rounds-with-drift", "cancelled",
    ];

    public static InterpretedRecipe Recipe { get; } = new(
        new RecipeVersion(EngineFixture.Step.Id, EngineFixture.Step.Version, EngineFixture.Step.Hash!.Value), // why: the shipped recipe is read with its hash
        EngineFixture.Step,
        EngineFixture.Catalog,
        new Dictionary<FactKey, InputFact> { [FactKey.From("fact:spec")] = new("spec.md", EngineFixture.Fact("the spec")) });

    public static InstanceView View(string name) => name switch
    {
        "desk-task" => Of(Produce(), Ago(72), timeoutAt: Now + new TimeSpan(days: 1, hours: 22, minutes: 48, seconds: 0)),
        "parked-no-progress-child" => Of(MirroringChild(), Ago(12), children: [new ChildView(InstanceAddress.From("T-3/step/1/step.4"), "parked: no-progress")]),
        "parked-budget" => Of(Park(ParkReason.Budget, ParkKind.Step, "produce used 200k of 200k tokens") with { Spent = Spent(200_000) }, Ago(3)),
        "escalated-gate" => Of(Escalated(), Ago(120)),
        "taken-over" => Of(TakenOver(TimeSpan.FromSeconds(40)), Ago(5)),
        "done" => Of(Ended("done", "resolved"), Ago(1)),
        "parked-rounds" => Of(Park(ParkReason.Rounds, ParkKind.Loop, "revisions used 3 of 3 rounds"), Ago(30)),
        "parked-needs-answer-step" => Of(Park(ParkReason.NeedsAnswer, ParkKind.Step, "step.produce failed: the spec is ambiguous"), Ago(8)),
        "parked-needs-answer-wait" => Of(Park(ParkReason.NeedsAnswer, ParkKind.Wait, "gate.test.check refused produce -> checks (no output)"), Ago(8)),
        "parked-timeout" => Of(Park(ParkReason.Timeout, kind: null, "produce ran past 2d"), Ago(60 * 26)),
        "parked-revoked" => Of(Park(ParkReason.SpecConflict, ParkKind.Revoked, "workflow.step was revoked."), Ago(4)),
        "parked-dispatch-no-runtime" => Of(DispatchPark("effect.claim.ref can't run: repo 'x' isn't in the approved profile (SND-WF-010). Fix that, then resume to send it again."), Ago(4)),
        "parked-dispatch-effect-failed" => Of(DispatchPark("effect.claim.ref can't run: the ref could not be created. Fix that, then resume to send it again."), Ago(4)),
        "parked-dispatch-allowlist" => Of(DispatchPark("effect.claim.ref isn't on the profile's effect allowlist. Add it and approve the profile, then resume to send it again."), Ago(4)),
        "parked-drift" => Of(Park(ParkReason.SpecConflict, ParkKind.Drift, "step.produce"), Ago(4)),
        "parked-no-runtime" => Of(Park(ParkReason.NoRuntime, kind: null, "step.test.agent"), Ago(2)),
        "parked-rounds-with-drift" => Of(Park(ParkReason.Rounds, ParkKind.Loop, "revisions used 3 of 3 rounds") with { PendingPark = Suspended(ParkReason.SpecConflict, ParkKind.Drift, "step.produce") }, Ago(30)),
        "cancelled" => Of(Ended("cancelled", "cancelled"), Ago(1)),
        "parked-rounds-with-approved-drift" => Of(Park(ParkReason.Rounds, ParkKind.Loop, "revisions used 3 of 3 rounds") with { PendingPark = Suspended(ParkReason.SpecConflict, ParkKind.Drift, "step.produce") with { Approved = true } }, Ago(30)),
        "parked-rounds-with-later-drift" => Of(Park(ParkReason.Rounds, ParkKind.Loop, "revisions used 3 of 3 rounds") with
        {
            PendingPark = Suspended(ParkReason.Timeout, kind: null, "produce waited too long"),
            LaterParks = [Suspended(ParkReason.SpecConflict, ParkKind.Drift, "step.produce")],
        }, Ago(30)),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "no such status case"),
    };

    private static InstanceView Of(WorkflowInstance instance, DateTimeOffset since, DateTimeOffset? timeoutAt = null, IReadOnlyList<ChildView>? children = null) =>
        InstanceViews.Of(instance, Recipe, since, timeoutAt, new InstanceViewContext(Version: 4, Children: children));

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
        Suspension = Suspended(ParkReason.NoProgress, ParkKind.Loop, "checks failed twice with the same findings") with { Mirrors = EngineFixture.ChildId },
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
