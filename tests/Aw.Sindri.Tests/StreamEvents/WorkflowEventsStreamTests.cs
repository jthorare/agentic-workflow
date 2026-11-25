using System.Text.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Features.StreamEvents;

namespace Aw.Sindri.Tests.StreamEvents;

public sealed class WorkflowEventsStreamTests
{
    private static readonly WorkflowId _wf = WorkflowId.From("WF-" + 1.ToString("D32", System.Globalization.CultureInfo.InvariantCulture));
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("drift-check");
    private static readonly WorkflowRunId _run = WorkflowRunId.From("drift-check@1:1");
    private static readonly NodeId _node = NodeId.From("fetch");

    [Fact, Trait("Lane", "unit")]
    public async Task Workflow_lifecycle_events_reach_the_stream_with_ids_only()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var changed = new WorkflowChangedPayload(_wf, _slug, 3, "sha256:secret-spec-hash", "human:cli");
        var types = new[] { EventTypes.WorkflowCreated, EventTypes.WorkflowRevised, EventTypes.WorkflowActivated, EventTypes.WorkflowPaused, EventTypes.WorkflowArchived };
        foreach (var type in types)
        {
            await Append(h, grant, type, changed, type + ":k");
        }

        var page = (await StreamEventsServices.Over(h).Handle(new StreamEventsRequest(After: 0), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(types, page.Events.Select(e => e.Type), StringComparer.Ordinal);
        Assert.All(page.Events, e => Assert.Equal(["revision", "slug", "workflowId"], Names(e.Payload)));
        Assert.All(page.Events, e => Assert.Equal(3, e.Payload.GetProperty("revision").GetInt32()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Workflow_run_and_node_events_reach_the_stream_with_ids_only()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        await Append(h, grant, EventTypes.WorkflowRunStarted, new WorkflowRunStartedPayload(_run, _wf, 1, "manual: secret trigger", "human:cli", WorkflowRunMode.Live), "run.started");
        await Append(h, grant, EventTypes.WorkflowRunFinished, new WorkflowRunFinishedPayload(_run, "failed", "secret error", "engine", WorkflowRunMode.Live), "run.finished");
        await Append(h, grant, EventTypes.WorkflowRunCancelled, new WorkflowRunCancelledPayload(_run, "secret reason", "human:cli", WorkflowRunMode.Live), "run.cancelled");
        await Append(h, grant, EventTypes.WorkflowNodeStarted, new WorkflowNodeStartedPayload(_run, _node, 2, "engine"), "node.started");
        await Append(h, grant, EventTypes.WorkflowNodeFinished, new WorkflowNodeFinishedPayload(_run, _node, 2, "ok", Truncated: true, "engine"), "node.finished");
        await Append(h, grant, EventTypes.WorkflowNodeFailed, new WorkflowNodeFailedPayload(_run, _node, 2, "secret error", "engine"), "node.failed");

        var page = (await StreamEventsServices.Over(h).Handle(new StreamEventsRequest(After: 0), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(6, page.Events.Count);
        Assert.All(page.Events.Take(3), e => Assert.Equal(["mode", "runId", "status"], Names(e.Payload)));   // why: the public run form carries the state and mode (Workflows page P0); status is null on started and cancelled
        Assert.All(page.Events.Skip(3), e => Assert.Equal(["itemIndex", "nodeId", "runId"], Names(e.Payload)));
        Assert.All(page.Events, e => Assert.Equal(_run.Value, e.Payload.GetProperty("runId").GetString()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Taking_over_and_handing_back_a_node_stay_off_the_stream()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        await Append(h, grant, EventTypes.WorkflowNodeTakenOver, new WorkflowNodeTakenOverPayload(_run, _node, 0, "human:cli"), "taken-over");
        await Append(h, grant, EventTypes.WorkflowNodeHandedBack, new WorkflowNodeHandedBackPayload(_run, _node, 0, "human:cli"), "handed-back");

        var page = (await StreamEventsServices.Over(h).Handle(new StreamEventsRequest(After: 0), TestContext.Current.CancellationToken)).Value;

        Assert.Empty(page.Events);
    }

    private static string[] Names(JsonElement payload) => [.. payload.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)];

    private static async Task Append<TPayload>(SliceHarness h, LeaseGrant grant, string type, TPayload payload, string key) =>
        _ = await h.Store.Append(h.Events.Entry(type, payload, key), grant, TestContext.Current.CancellationToken);
}
