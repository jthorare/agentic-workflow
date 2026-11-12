using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApiGoldens;

namespace Aw.Sindri.Tests.Events;

/// <summary>
/// The ledger payload of each graph run event (issue 2258, PR 6), pinned as a golden through the context the ledger writes with: a rename or a change of casing
/// would corrupt rows that can never be changed. AW_GOLDEN_UPDATE=1 rewrites a file.
/// </summary>
public sealed class WorkflowRunEventGoldenTests
{
    private static readonly WorkflowRunId _run = WorkflowRunId.From("nightly@3:12");
    private static readonly NodeId _fetch = NodeId.From("fetch");
    private static readonly NodeId _notify = NodeId.From("notify");
    private static readonly NodeId _agent = NodeId.From("agent");

    public static TheoryData<string> Cases =>
    [
        EventTypes.WorkflowRunStarted,
        EventTypes.WorkflowRunFinished,
        EventTypes.WorkflowRunCancelled,
        EventTypes.WorkflowNodeStarted,
        EventTypes.WorkflowNodeFinished,
        EventTypes.WorkflowNodeFailed,
        EventTypes.WorkflowNodeTakenOver,
        EventTypes.WorkflowNodeHandedBack,
    ];

    private static string Of<T>(SliceHarness h, string type, T payload) => ApiGolden.Reindent(h.Events.Entry(type, payload, "key", A.FirstItemId.Value).Envelope.Payload);

    private static string Json(SliceHarness h, string type) => type switch
    {
        EventTypes.WorkflowRunStarted => Of(h, type, new WorkflowRunStartedPayload(_run, WorkflowId.From("WF-00000000000000000000000000000001"), 3, "{\"kind\":\"manual\"}", "human", Aw.Sindri.Core.Graph.Runs.WorkflowRunMode.Live)),
        EventTypes.WorkflowRunFinished => Of(h, type, new WorkflowRunFinishedPayload(_run, "failed", "node 'fetch' failed: 502", "service:workflow", Aw.Sindri.Core.Graph.Runs.WorkflowRunMode.Live)),
        EventTypes.WorkflowRunCancelled => Of(h, type, new WorkflowRunCancelledPayload(_run, "owner stopped it", "human", Aw.Sindri.Core.Graph.Runs.WorkflowRunMode.Live)),
        EventTypes.WorkflowNodeStarted => Of(h, type, new WorkflowNodeStartedPayload(_run, _fetch, 0, "service:workflow")),
        EventTypes.WorkflowNodeFinished => Of(h, type, new WorkflowNodeFinishedPayload(_run, _fetch, 2, "succeeded", Truncated: true, "service:workflow")),
        EventTypes.WorkflowNodeFailed => Of(h, type, new WorkflowNodeFailedPayload(_run, _notify, 0, "no channel", "service:workflow")),
        EventTypes.WorkflowNodeTakenOver => Of(h, type, new WorkflowNodeTakenOverPayload(_run, _agent, 2, "human:cli")),
        EventTypes.WorkflowNodeHandedBack => Of(h, type, new WorkflowNodeHandedBackPayload(_run, _agent, 2, "session:launch:LCH-00000000000000000000000000000001")),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Cases))]
    public void The_ledger_payload_equals_its_golden(string type) =>
        Golden.Match(Json(new SliceHarness(), type), typeof(WorkflowRunEventGoldenTests).Assembly, $"Golden/ledger/{type}.json", Normalization.None);

    [Fact, Trait("Lane", "unit")]
    public void Every_graph_run_event_has_a_golden() =>
        Assert.All(
            EventCatalog.All.Select(static e => e.Type).Where(static t => t.StartsWith("workflow.run.", StringComparison.Ordinal) || t.StartsWith("workflow.node.", StringComparison.Ordinal)),
            type => Assert.Contains(Cases, c => string.Equals(c.Data, type, StringComparison.Ordinal)));
}
