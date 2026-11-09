using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of 'POST /sindri/workflows/{slug}/runs', 'POST /sindri/workflow-runs/{run}/cancel' (issue 2258, PR 6) and 'POST /sindri/workflow-runs/{run}/nodes/{node}/answer' (PR 9): the refusals, which need no Temporal
/// server. A started run needs one, so its answer is pinned by the service tests. Each scenario seeds the workflow or run it needs through the host's stores.
/// </summary>
internal static class WorkflowRunScenarios
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");
    private static readonly WorkflowRunId _ended = WorkflowRunId.Of(_slug, 1, 1);
    private const string AnswerSlug = "workflow-runs-run-nodes-node-answer";
    private const string AnswerBody = "{\"text\":\"yes\"}";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("workflows-slug-runs", "unknown-workflow"), ("workflows-slug-runs", "bad-slug"), ("workflows-slug-runs", "not-active"), ("workflows-slug-runs", "bad-payload"),
        ("workflow-runs-run-cancel", "not-found"), ("workflow-runs-run-cancel", "bad-id"), ("workflow-runs-run-cancel", "already-ended"),
        (AnswerSlug, "not-found"), (AnswerSlug, "bad-id"), (AnswerSlug, "already-ended"), (AnswerSlug, "answered"),
    ];

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
        if (string.Equals(slug, AnswerSlug, StringComparison.Ordinal))
        {
            return await Answer(host, scenario, human, ct);
        }

        switch ((slug, scenario))
        {
            case ("workflows-slug-runs", "unknown-workflow"):
                return await Ask(host, Post("/sindri/workflows/nope/runs", json: null, human));
            case ("workflows-slug-runs", "bad-slug"):
                return await Ask(host, Post("/sindri/workflows/Not_A_Slug/runs", json: null, human));
            case ("workflows-slug-runs", "not-active"):
                await Create(host, activate: false, ct);
                return await Ask(host, Post($"/sindri/workflows/{_slug.Value}/runs", json: null, human));
            case ("workflows-slug-runs", "bad-payload"):
                await Create(host, activate: true, ct);
                return await Ask(host, Post($"/sindri/workflows/{_slug.Value}/runs", "{\"payload\":\"[1, 2]\"}", human));
            case ("workflow-runs-run-cancel", "bad-id"):
                return await Ask(host, Post("/sindri/workflow-runs/not-a-run/cancel", json: null, human));
            case ("workflow-runs-run-cancel", "already-ended"):
                await EndedRun(host, ct);
                return await Ask(host, Post($"/sindri/workflow-runs/{Uri.EscapeDataString(_ended.Value)}/cancel", json: null, human));
            default:
                return await Ask(host, Post($"/sindri/workflow-runs/{Uri.EscapeDataString(_ended.Value)}/cancel", json: null, human));
        }
    }

    /// <summary>The host of the 'answered' scenario: the answerer is a fake that accepts, as Temporal is faked everywhere else.</summary>
    /// <param name="ct">Cancels the start.</param>
    /// <returns>The host.</returns>
    public static Task<GoldenHost> Start(CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, extra: static services => _ = services.Replace(ServiceDescriptor.Singleton<IGraphRunAnswerer>(new FakeGraphRunAnswerer())));

    private static async Task<string> Answer(GoldenHost host, string scenario, (string Name, string Value)[] human, CancellationToken ct)
    {
        switch (scenario)
        {
            case "bad-id":
                return await Ask(host, Post("/sindri/workflow-runs/not-a-run/nodes/ask/answer", AnswerBody, human));
            case "answered":
                _ = (await host.App.Services.GetRequiredService<IWorkflowRunStore>().Insert(new WorkflowRun(_ended, _id, 1, "{\"kind\":\"manual\"}", WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, host.Now(), EndedAt: null), ct)).Value;
                return await Ask(host, Post($"/sindri/workflow-runs/{Uri.EscapeDataString(_ended.Value)}/nodes/ask/answer", AnswerBody, human));
            case "already-ended":
                await EndedRun(host, ct);
                return await Ask(host, Post($"/sindri/workflow-runs/{Uri.EscapeDataString(_ended.Value)}/nodes/ask/answer", AnswerBody, human));
            default:
                return await Ask(host, Post($"/sindri/workflow-runs/{Uri.EscapeDataString(_ended.Value)}/nodes/ask/answer", AnswerBody, human));
        }
    }

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

    private static async Task EndedRun(GoldenHost host, CancellationToken ct)
    {
        var run = new WorkflowRun(_ended, _id, 1, "{\"kind\":\"manual\"}", WorkflowRunStatus.Succeeded, Outputs: null, Error: null, ItemId: null, host.Now(), EndedAt: null);
        _ = (await host.App.Services.GetRequiredService<IWorkflowRunStore>().Insert(run, ct)).Value;
    }

    private static async Task<string> Ask(GoldenHost host, HttpRequestMessage request) => (await host.Http.Ask(request)).Body;
}
