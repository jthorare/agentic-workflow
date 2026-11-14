using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of the workflow reads (issue 2258, PR 8a): 'GET /sindri/workflows', '/sindri/workflows/{slug}', '/sindri/workflows/{slug}/runs' and '/sindri/workflow-runs/{run}'. Each scenario seeds the
/// workflows and runs it reads through the host's stores. The slugs match the write goldens of the same route (<see cref="WorkflowRunScenarios"/>), whose scenario names differ.
/// </summary>
internal static class WorkflowReadScenarios
{
    private static readonly WorkflowId _morning = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowId _weekly = WorkflowId.From("WF-00000000000000000000000000000002");
    private static readonly WorkflowSlug _morningSlug = WorkflowSlug.From("morning-check");
    private static readonly WorkflowSlug _weeklySlug = WorkflowSlug.From("weekly-report");

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("workflows", "list-empty"), ("workflows", "list-two"), ("workflows", "list-include-archived"),
        ("workflows-slug", "found"), ("workflows-slug", "not-found"),
        ("workflows-slug", "found-latest"), ("workflows-slug", "revision-not-found"), ("workflows-slug", "revision-invalid"),
        ("workflows-slug-runs", "list-empty"), ("workflows-slug-runs", "list-three"),
        ("workflow-runs-run", "running"), ("workflow-runs-run", "finished"), ("workflow-runs-run", "not-found"),
        ("workflow-runs-run", "agent-waiting"), ("workflow-runs-run", "agent-taken-over"),
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
        switch ((slug, scenario))
        {
            case ("workflows", "list-empty"):
                return await Ask(host, Get("/sindri/workflows", human));
            case ("workflows", "list-two"):
                await Create(host, _morning, _morningSlug, "Morning check", activate: true, ct);
                await Create(host, _weekly, _weeklySlug, "Weekly report", activate: false, ct);
                return await Ask(host, Get("/sindri/workflows", human));
            case ("workflows", "list-include-archived"):
                // GET /sindri/workflows?includeArchived=true: the archived workflow is listed and marked (#2702); the default list above leaves it out.
                await Create(host, _morning, _morningSlug, "Morning check", activate: true, ct);
                await Create(host, _weekly, _weeklySlug, "Weekly report", activate: false, ct);
                _ = (await host.App.Services.GetRequiredService<IWorkflowStore>().Archive(_weekly, ct)).Value;
                return await Ask(host, Get("/sindri/workflows?includeArchived=true", human));
            case ("workflows-slug", "found"):
                await Create(host, _morning, _morningSlug, "Morning check", activate: true, ct);
                await Save(host, ct);
                return await Ask(host, Get($"/sindri/workflows/{_morningSlug.Value}", human));
            case ("workflows-slug", "found-latest"):
                // ?revision=latest: the same workflow as "found" (active at 1, newest 2) with the newest revision shown (#3303)
                await Create(host, _morning, _morningSlug, "Morning check", activate: true, ct);
                await Save(host, ct);
                return await Ask(host, Get($"/sindri/workflows/{_morningSlug.Value}?revision=latest", human));
            case ("workflows-slug", "revision-not-found"):
                await Create(host, _morning, _morningSlug, "Morning check", activate: true, ct);
                await Save(host, ct);
                return await Ask(host, Get($"/sindri/workflows/{_morningSlug.Value}?revision=9", human));
            case ("workflows-slug", "revision-invalid"):
                await Create(host, _morning, _morningSlug, "Morning check", activate: true, ct);
                return await Ask(host, Get($"/sindri/workflows/{_morningSlug.Value}?revision=soon", human));
            case ("workflows-slug", "not-found"):
                return await Ask(host, Get("/sindri/workflows/nope", human));
            case ("workflows-slug-runs", "list-empty"):
                await Create(host, _morning, _morningSlug, "Morning check", activate: true, ct);
                return await Ask(host, Get($"/sindri/workflows/{_morningSlug.Value}/runs", human));
            case ("workflows-slug-runs", "list-three"):
                await Create(host, _morning, _morningSlug, "Morning check", activate: true, ct);
                await ThreeRuns(host, ct);
                return await Ask(host, Get($"/sindri/workflows/{_morningSlug.Value}/runs", human));
            default:
                return await RunRead(host, scenario, human, ct);
        }
    }

    // GET /sindri/workflow-runs/{run}: the running run (its run_agent node 'agent' runs, so its row says 'waiting'), the same once the human took the node over ('taken_over': the held marker of its launch is set, as the workflow sets it),
    // the finished run, and an unknown one.
    private static async Task<string> RunRead(GoldenHost host, string scenario, (string Name, string Value)[] human, CancellationToken ct)
    {
        if (string.Equals(scenario, "not-found", StringComparison.Ordinal))
        {
            return await Ask(host, Get(RunRoute(9), human));
        }

        await Create(host, _morning, _morningSlug, "Morning check", activate: true, ct);
        if (string.Equals(scenario, "finished", StringComparison.Ordinal))
        {
            await FinishedRun(host, ct);
        }
        else
        {
            await RunningRun(host, ct);
        }

        if (string.Equals(scenario, "agent-taken-over", StringComparison.Ordinal))
        {
            host.App.Services.GetRequiredService<ILaunchFiles>().MarkTakenOver(NodeRunAddress.Of(WorkflowRunId.Of(_morningSlug, 1, 1), NodeId.From("agent")).Launch(), taken: true);
        }

        return await Ask(host, Get(RunRoute(1), human));
    }

    private static string RunRoute(int occurrence) => $"/sindri/workflow-runs/{Uri.EscapeDataString(WorkflowRunId.Of(_morningSlug, 1, occurrence).Value)}";

    private static GraphSpec Spec(string name) =>
        new(Version: 1, name, Description: "Checks the repos every morning", Concurrency: null, Resources: null,
            Nodes:
            [
                new GraphNode(NodeId.From("start"), "manual", Title: null, []),
                new GraphNode(NodeId.From("agent"), "run_agent", "Check the repos", new JsonObject { ["prompt"] = "Look for drift" }),
            ],
            Edges: [new GraphEdge(NodeId.From("start"), Output: null, NodeId.From("agent"))]);

    private static async Task Create(GoldenHost host, WorkflowId id, WorkflowSlug slug, string name, bool activate, CancellationToken ct)
    {
        var workflows = host.App.Services.GetRequiredService<IWorkflowStore>();
        _ = (await workflows.Create(id, slug, Spec(name), "owner", ct)).Value;
        if (activate)
        {
            _ = (await workflows.Activate(id, 1, ct)).Value;
        }
    }

    private static async Task Save(GoldenHost host, CancellationToken ct) =>
        _ = (await host.App.Services.GetRequiredService<IWorkflowStore>().Save(_morning, 1, Spec("Morning check"), "session:golden", ct)).Value;

    private static WorkflowRun Run(GoldenHost host, int occurrence, WorkflowRunStatus status, string kind, string? error) =>
        new(WorkflowRunId.Of(_morningSlug, 1, occurrence), _morning, 1, $"{{\"kind\":\"{kind}\"}}", status, Outputs: null, error, ItemId: null, host.Now().AddMinutes(occurrence), EndedAt: null);

    private static async Task ThreeRuns(GoldenHost host, CancellationToken ct)
    {
        var runs = host.App.Services.GetRequiredService<IWorkflowRunStore>();
        _ = (await runs.Insert(Run(host, 1, WorkflowRunStatus.Succeeded, "manual", error: null), ct)).Value;
        _ = (await runs.Insert(Run(host, 2, WorkflowRunStatus.Failed, "schedule", "node 'agent' failed"), ct)).Value;
        _ = (await runs.Insert(Run(host, 3, WorkflowRunStatus.Running, "manual", error: null), ct)).Value;
    }

    private static async Task RunningRun(GoldenHost host, CancellationToken ct)
    {
        var runs = host.App.Services.GetRequiredService<IWorkflowRunStore>();
        _ = (await runs.Insert(Run(host, 1, WorkflowRunStatus.Running, "manual", error: null), ct)).Value;
        _ = (await runs.RecordNodeRun(new NodeRun(WorkflowRunId.Of(_morningSlug, 1, 1), NodeId.From("start"), 0, NodeRunStatus.Succeeded, Input: null, Output: "{\"kind\":\"manual\"}", Truncated: false, Error: null, Usage: null), ct)).Value;
        _ = (await runs.RecordNodeRun(new NodeRun(WorkflowRunId.Of(_morningSlug, 1, 1), NodeId.From("agent"), 0, NodeRunStatus.Running, Input: "{\"prompt\":\"Look for drift\"}", Output: null, Truncated: false, Error: null, Usage: null), ct)).Value;
    }

    private static async Task FinishedRun(GoldenHost host, CancellationToken ct)
    {
        await RunningRun(host, ct);
        var runs = host.App.Services.GetRequiredService<IWorkflowRunStore>();
        var id = WorkflowRunId.Of(_morningSlug, 1, 1);
        _ = (await runs.RecordNodeRun(new NodeRun(id, NodeId.From("agent"), 0, NodeRunStatus.Succeeded, Input: "{\"prompt\":\"Look for drift\"}", Output: "{\"text\":\"no drift\"}", Truncated: false, Error: null, Usage: null), ct)).Value;
        _ = (await runs.SetStatus(id, WorkflowRunStatus.Succeeded, "{\"summary\":\"no drift\"}", error: null, ct)).Value;
    }

    private static async Task<string> Ask(GoldenHost host, HttpRequestMessage request) => (await host.Http.Ask(request)).Body;
}
