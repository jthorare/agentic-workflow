using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P11's engine-state goldens: the queue and the item detail of a running and a parked item, and the needs-you rows of a park, an open desk task and an
/// idle takeover. Each scenario makes an item through the API and appends the workflow rows the engine would write for it, so the bodies come from the real
/// projections and routes (the engine itself is the proof's job).
/// </summary>
internal static class ParkScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("queue", "running"), ("queue", "parked"), ("items-id", "running"),
        ("needs-you", "park"), ("needs-you", "park-mirrored-child"), ("needs-you", "park-nested"), ("needs-you", "park-nested-refused"), ("needs-you", "desk-task"), ("needs-you", "idle-takeover"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> (started on the item scenarios' profile) and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        var item = await ItemScenarios.Create(host, "Try the engine", "demo");
        await ViewScenarios.CatchUp(host, ct);
        var address = item + "/step/1";
        switch (scenario)
        {
            case "running":
                await Append(host, item, EventTypes.WorkflowStarted, Row(address, "triaged", "start", new FrameSummary("triaged", 1, Run: null, Child: null), suspension: null), "started", ct);
                break;
            case "desk-task":
                await Append(host, item, EventTypes.WorkflowStarted, Row(address, "produce", "start", Desk(), suspension: null), "started", ct);
                break;
            case "parked" or "park":
                await Append(host, item, EventTypes.WorkflowStarted, Row(address, "produce", "start", Desk(), suspension: null), "started", ct);
                var park = new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null);
                await Append(host, item, EventTypes.WorkflowTransition, Row(address, "produce", "step.completed", frame: null, park), "parked", ct);
                break;
            case "park-mirrored-child":
                await Append(host, item, EventTypes.WorkflowStarted, Row(address, "running", "start", new FrameSummary("running", 1, Run: null, Child: null), suspension: null), "started", ct);
                var childAddress = address + "/step.4";
                var mirrored = new SuspensionSummary(SuspensionKind.Park, ParkReason.NeedsAnswer, ParkKind.Step, Escalation: null, Mirrors: WorkflowInstanceId.Of(InstanceAddress.From(childAddress)).Value, MirrorsAddress: childAddress);
                await Append(host, item, EventTypes.WorkflowTransition, Row(address, "running", "child.parked", frame: null, mirrored), "parked", ct);
                break;
            case "park-nested" or "park-nested-refused":
                await ParkNested(host, item, address, refused: scenario == "park-nested-refused", ct);
                break;
            default:   // 'idle-takeover'
                await Append(host, item, EventTypes.WorkflowStarted, Row(address, "produce", "start", Desk(), suspension: null), "started", ct);
                var takeover = new SuspensionSummary(SuspensionKind.Takeover, Reason: null, ParkKind: null, Escalation: null, Mirrors: null);
                await Append(host, item, EventTypes.WorkflowTransition, Row(address, "produce", TriggerNames.Takeover, frame: null, takeover), "takeover", ct);
                await Append(host, item, EventTypes.WorkflowNotified, Row(address, "produce", TriggerNames.TakeoverIdle, frame: null, takeover), "idle", ct);
                break;
        }

        await ViewScenarios.CatchUp(host, ct);
        var route = slug == "items-id" ? "/sindri/items/" + item : "/sindri/" + slug;
        return (await host.Http.Ask(Get(route, ItemScenarios.Human(host)))).Body;
    }

    // The root runs; a descendant parks (needs-answer, or a refused effect), and the root's row points at it.
    private static async Task ParkNested(GoldenHost host, string item, string address, bool refused, CancellationToken ct)
    {
        await Append(host, item, EventTypes.WorkflowStarted, Row(address, "running", "start", new FrameSummary("running", 1, Run: null, Child: null), suspension: null), "started", ct);
        var nested = refused
            ? new SuspensionSummary(SuspensionKind.Park, ParkReason.SpecConflict, ParkKind.Dispatch, Escalation: null, Mirrors: null)
            : new SuspensionSummary(SuspensionKind.Park, ParkReason.NeedsAnswer, ParkKind.Step, Escalation: null, Mirrors: null);
        var refusal = refused ? "SND-WF-010: fact:nextPhase must be { phase, attempt, packet, base, stackParent } with a lowercase phase id and an attempt from 1" : null;
        await Append(host, item, EventTypes.WorkflowTransition, Row(address + "/phase.p1.1", "running", "step.completed", frame: null, nested, refusal), "nested-parked", ct);
    }

    /// <summary>Starts the item's instance in 'triaged', as the 'running' scenario does.</summary>
    internal static async Task StartRunning(GoldenHost host, string item, CancellationToken ct)
    {
        await Append(host, item, EventTypes.WorkflowStarted, Row(item + "/step/1", "triaged", "start", new FrameSummary("triaged", 1, Run: null, Child: null), suspension: null), "started", ct);
        await ViewScenarios.CatchUp(host, ct);
    }

    private static FrameSummary Desk() => new("produce", 1, "1.1", Child: null, StepRuntime.Desk);

    private static Task Append(GoldenHost host, string item, string type, WorkflowEventPayload payload, string key, CancellationToken ct) =>
        ItemScenarios.AppendFor(host, item, type, payload, $"{type}:golden:{key}", ct);

    private static WorkflowEventPayload Row(string address, string to, string trigger, FrameSummary? frame, SuspensionSummary? suspension, string? reason = null)
    {
        var delta = new WorkflowEventDelta(frame, suspension, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, Outcome: null);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: reason, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(address, "from", to, trigger, Outcome: null, Run: null, detail);
    }
}
