using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// Appends the workflow rows the old engine wrote for a running item. The launch goldens still start one this way; the views no longer read these rows
/// (v2 PR 4b retired the engine part), and the Parks goldens that showed them are gone.
/// </summary>
internal static class ParkScenarios
{
    /// <summary>Appends a started instance in 'triaged' for the item, then waits for the views.</summary>
    internal static async Task StartRunning(GoldenHost host, string item, CancellationToken ct)
    {
        await Append(host, item, EventTypes.WorkflowStarted, Row(item + "/step/1", "triaged", "start", new FrameSummary("triaged", 1, Run: null, Child: null), suspension: null), "started", ct);
        await ViewScenarios.CatchUp(host, ct);
    }

    private static Task Append(GoldenHost host, string item, string type, WorkflowEventPayload payload, string key, CancellationToken ct) =>
        ItemScenarios.AppendFor(host, item, type, payload, $"{type}:golden:{key}", ct);

    private static WorkflowEventPayload Row(string address, string to, string trigger, FrameSummary? frame, SuspensionSummary? suspension)
    {
        var delta = new WorkflowEventDelta(frame, suspension, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, Outcome: null);
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.From("WFI-" + new string('a', 32)), "t"), "human:cli", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null,
            Answer: null, Amount: null, Reason: null, [], [], Grant: null, Override: null, Refusal: null, [], delta);
        return new WorkflowEventPayload(address, "from", to, trigger, Outcome: null, Run: null, detail);
    }
}
