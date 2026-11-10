using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of 'POST /sindri/workflows/{slug}/runs' and 'POST /sindri/workflow-runs/{run}/cancel' (issue 2258, PR 6): the refusals, which need no Temporal
/// server. A started run needs one, so its answer is pinned by the service tests. Each scenario seeds the workflow or run it needs through the host's stores.
/// The takeover and handback of a 'run_agent' node (#2472) run on a host whose <see cref="IAgentNodeControl"/> is a <see cref="FakeAgentNodeControl"/>,
/// so their success answers need no Temporal either.
/// </summary>
internal static class WorkflowRunScenarios
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");
    private static readonly WorkflowRunId _ended = WorkflowRunId.Of(_slug, 1, 1);
    private static readonly WorkflowRunId _queued = WorkflowRunId.Of(_slug, 1, 2);
    private const string TakeOverSlug = "workflow-runs-run-nodes-node-takeover";
    private const string HandBackSlug = "workflow-runs-run-nodes-node-handback";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("workflows-slug-runs", "unknown-workflow"), ("workflows-slug-runs", "bad-slug"), ("workflows-slug-runs", "not-active"), ("workflows-slug-runs", "bad-payload"), ("workflows-slug-runs", "empty-body"),
        ("workflow-runs-run-cancel", "not-found"), ("workflow-runs-run-cancel", "bad-id"), ("workflow-runs-run-cancel", "already-ended"), ("workflow-runs-run-cancel", "cancelled"),
        (TakeOverSlug, "taken-over"), (TakeOverSlug, "not-waiting"), (TakeOverSlug, "bad-id"), (TakeOverSlug, "not-found"), (TakeOverSlug, "already-ended"), (TakeOverSlug, "temporal-down"),
        (HandBackSlug, "handed-back"), (HandBackSlug, "not-taken-over"), (HandBackSlug, "not-waiting"),
    ];

    /// <summary>Whether the scenario runs on <see cref="Start"/>'s host (the takeover and handback routes).</summary>
    /// <param name="slug">The route.</param>
    /// <returns>True for the takeover and handback routes.</returns>
    public static bool NeedsAgentControl(string slug) => string.Equals(slug, TakeOverSlug, StringComparison.Ordinal) || string.Equals(slug, HandBackSlug, StringComparison.Ordinal);

    /// <summary>The golden host with a <see cref="FakeAgentNodeControl"/> where Temporal's control would be.</summary>
    /// <param name="ct">Cancels the start.</param>
    /// <returns>The started host.</returns>
    public static Task<GoldenHost> Start(CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, extra: services =>
        {
            var control = new FakeAgentNodeControl();
            _ = services.AddSingleton(control);
            _ = services.Replace(ServiceDescriptor.Singleton<IAgentNodeControl>(control));
        });

    /// <summary>Whether this class owns the scenario.</summary>
    /// <param name="slug">The route.</param>
    /// <param name="scenario">The scenario.</param>
    /// <returns>True when it does.</returns>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the answer's body.</summary>
    /// <param name="host">The golden host.</param>
    /// <param name="slug">The route.</param>
    /// <param name="scenario">The scenario.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The body.</returns>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        var human = Human(host.Token.Value);
        switch ((slug, scenario))
        {
            case ("workflows-slug-runs", "unknown-workflow"):
                return await Ask(host, Post("/sindri/workflows/nope/runs", json: null, human));
            case ("workflows-slug-runs", "bad-slug"):
                return await Ask(host, Post("/sindri/workflows/Not_A_Slug/runs", json: null, human));
            case ("workflows-slug-runs", "not-active"):
                await Create(host, activate: false, ct);
                return await Ask(host, Post($"/sindri/workflows/{_slug.Value}/runs", json: null, human));
            case ("workflows-slug-runs", "empty-body"):
                // why: a body that leaves 'payload' out reaches the service (#2493); before, binding refused it with a bodiless 400
                await Create(host, activate: false, ct);
                return await Ask(host, Post($"/sindri/workflows/{_slug.Value}/runs", "{}", human));
            case ("workflows-slug-runs", "bad-payload"):
                await Create(host, activate: true, ct);
                return await Ask(host, Post($"/sindri/workflows/{_slug.Value}/runs", "{\"payload\":\"[1, 2]\"}", human));
            case ("workflow-runs-run-cancel", "bad-id"):
                return await Ask(host, Post("/sindri/workflow-runs/not-a-run/cancel", json: null, human));
            case ("workflow-runs-run-cancel", "already-ended"):
                await EndedRun(host, ct);
                return await Ask(host, Post($"/sindri/workflow-runs/{Uri.EscapeDataString(_ended.Value)}/cancel", json: null, human));
            case ("workflow-runs-run-cancel", "cancelled"):
                // why: a queued run is cancelled in the store with its workflow.run.cancelled event, so no Temporal worker is needed
                await QueuedRun(host, ct);
                return await Ask(host, Post($"/sindri/workflow-runs/{Uri.EscapeDataString(_queued.Value)}/cancel", json: null, human));
            case (TakeOverSlug, _) or (HandBackSlug, _):
                return await AgentNode(host, slug, scenario, human, ct);
            default:
                return await Ask(host, Post($"/sindri/workflow-runs/{Uri.EscapeDataString(_ended.Value)}/cancel", json: null, human));
        }
    }

    // A run that waits on the node 'agent' (a running run) unless the scenario says otherwise; the fake control holds the node the way the workflow would.
    private static async Task<string> AgentNode(GoldenHost host, string slug, string scenario, (string Name, string Value)[] human, CancellationToken ct)
    {
        var control = host.App.Services.GetRequiredService<FakeAgentNodeControl>();
        var route = slug == TakeOverSlug ? "takeover" : "handback";
        var agent = NodeId.From("agent");
        switch (scenario)
        {
            case "bad-id":
                return await Ask(host, Post($"/sindri/workflow-runs/not-a-run/nodes/agent/{route}", json: null, human));
            case "not-found":
                return await Ask(host, Post(AgentRoute(_queued, "agent", route), "{\"itemIndex\":0}", human));
            case "already-ended":
                await EndedRun(host, ct);
                return await Ask(host, Post(AgentRoute(_ended, "agent", route), "{\"itemIndex\":0}", human));
            case "temporal-down":
                await QueuedRun(host, ct, WorkflowRunStatus.Running);
                control.Fail = Core.SindriErrors.EngineUnavailable.With("the Temporal server is not reachable");
                return await Ask(host, Post(AgentRoute(_queued, "agent", route), "{\"itemIndex\":0}", human));
            case "not-waiting":
                await QueuedRun(host, ct, WorkflowRunStatus.Running);
                control.Wait(NodeId.From("review"), takenOver: true);   // another node is the one the run waits on
                return await Ask(host, Post(AgentRoute(_queued, "agent", route), "{\"itemIndex\":0}", human));
            default:
                // taken-over: the node is free and the human takes it; not-taken-over and handed-back: the node is free, or held
                await QueuedRun(host, ct, WorkflowRunStatus.Running);
                control.Wait(agent, takenOver: scenario == "handed-back");
                return await Ask(host, Post(AgentRoute(_queued, "agent", route), "{\"itemIndex\":0}", human));
        }
    }

    private static string AgentRoute(WorkflowRunId run, string node, string verb) => $"/sindri/workflow-runs/{Uri.EscapeDataString(run.Value)}/nodes/{node}/{verb}";

    private static async Task Create(GoldenHost host, bool activate, CancellationToken ct)
    {
        var spec = new GraphSpec(Version: 1, Name: "Morning check", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        var workflows = host.App.Services.GetRequiredService<IWorkflowStore>();
        _ = (await workflows.Create(_id, _slug, spec, "owner", ct)).Value;
        if (activate)
        {
            _ = (await workflows.Activate(_id, 1, ct)).Value;
        }
    }

    private static async Task QueuedRun(GoldenHost host, CancellationToken ct, WorkflowRunStatus status = WorkflowRunStatus.Queued)
    {
        var run = new WorkflowRun(_queued, _id, 1, "{\"kind\":\"manual\"}", status, Outputs: null, Error: null, ItemId: null, host.Now(), EndedAt: null);
        _ = (await host.App.Services.GetRequiredService<IWorkflowRunStore>().Insert(run, ct)).Value;
    }

    private static async Task EndedRun(GoldenHost host, CancellationToken ct)
    {
        var run = new WorkflowRun(_ended, _id, 1, "{\"kind\":\"manual\"}", WorkflowRunStatus.Succeeded, Outputs: null, Error: null, ItemId: null, host.Now(), EndedAt: null);
        _ = (await host.App.Services.GetRequiredService<IWorkflowRunStore>().Insert(run, ct)).Value;
    }

    private static async Task<string> Ask(GoldenHost host, HttpRequestMessage request) => (await host.Http.Ask(request)).Body;
}
