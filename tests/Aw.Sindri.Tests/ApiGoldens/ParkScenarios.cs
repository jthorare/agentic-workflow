using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
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
            default:   // 'parked'
                await Append(host, item, EventTypes.WorkflowStarted, Row(address, "produce", "start", Desk(), suspension: null), "started", ct);
                var park = new SuspensionSummary(SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, Escalation: null, Mirrors: null);
                await Append(host, item, EventTypes.WorkflowTransition, Row(address, "produce", "step.completed", frame: null, park), "parked", ct);
                break;
        }

        await ViewScenarios.CatchUp(host, ct);
        var route = slug == "items-id" ? "/sindri/items/" + item : "/sindri/" + slug;
        return (await host.Http.Ask(Get(route, ItemScenarios.Human(host)))).Body;
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
