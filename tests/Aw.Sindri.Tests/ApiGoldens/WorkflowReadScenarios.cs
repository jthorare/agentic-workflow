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
        ("workflows-slug", "found"), ("workflows-slug", "found-schedule"), ("workflows-slug", "not-found"),
        ("workflows-slug", "found-latest"), ("workflows-slug", "revision-not-found"), ("workflows-slug", "revision-invalid"),
        ("workflows-slug-runs", "list-empty"), ("workflows-slug-runs", "list-three"), ("workflows-slug-runs", "list-filtered"), ("workflows-slug-runs", "filter-invalid"),
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
            case ("workflows-slug", not "not-found"):
                return await SlugRead(host, scenario, human, ct);
            case ("workflows-slug", "not-found"):
                return await Ask(host, Get("/sindri/workflows/nope", human));
            case ("workflows-slug-runs", _):
                return await RunsRead(host, scenario, human, ct);
            default:
                return await RunRead(host, scenario, human, ct);
        }
    }

    // GET /sindri/workflows/{slug}[?revision=N|latest] on the Morning check workflow (active at 1); every scenario but "revision-invalid" saves revision 2 first.
    private static async Task<string> SlugRead(GoldenHost host, string scenario, (string Name, string Value)[] human, CancellationToken ct)
    {
        if (string.Equals(scenario, "found-schedule", StringComparison.Ordinal))
        {
            // an active weekday 7:00 UTC schedule: the clock reads Thu 1 Jan 2026 00:00 UTC, so nextRunAt is 07:00 that day, the value the list row shows (#3756)
            _ = (await host.App.Services.GetRequiredService<IWorkflowStore>().Create(_morning, _morningSlug, ScheduleSpec("Morning check"), "owner", ct)).Value;
            _ = (await host.App.Services.GetRequiredService<IWorkflowStore>().Activate(_morning, 1, ct)).Value;
            return await Ask(host, Get($"/sindri/workflows/{_morningSlug.Value}", human));
        }

        await Create(host, _morning, _morningSlug, "Morning check", activate: true, ct);
        if (!string.Equals(scenario, "revision-invalid", StringComparison.Ordinal))
        {
            await Save(host, ct);
        }

        // "found-latest": the same workflow as "found" with the newest revision shown (#3303)
        var query = scenario switch
        {
            "found-latest" => "?revision=latest",
            "revision-not-found" => "?revision=9",
            "revision-invalid" => "?revision=soon",
            _ => string.Empty,
        };
        return await Ask(host, Get($"/sindri/workflows/{_morningSlug.Value}{query}", human));
    }

    // GET /sindri/workflows/{slug}/runs on the Morning check workflow: no runs, three runs, the #4334 filtered read and a filter value it can't read.
    private static async Task<string> RunsRead(GoldenHost host, string scenario, (string Name, string Value)[] human, CancellationToken ct)
    {
        await Create(host, _morning, _morningSlug, "Morning check", activate: true, ct);
        switch (scenario)
        {
            case "list-three":
                await ThreeRuns(host, ct);
                return await Ask(host, Get($"/sindri/workflows/{_morningSlug.Value}/runs", human));
            case "list-filtered":
                // #4334: the failed and cancelled runs that started at or after 00:02; run 1 (succeeded), run 3 (running) and run 5 (failed, before the window) are left out
                await FiveRuns(host, ct);
                return await Ask(host, Get($"/sindri/workflows/{_morningSlug.Value}/runs?status=failed,cancelled&since=2026-01-01T00:02:00Z", human));
            case "filter-invalid":
                return await Ask(host, Get($"/sindri/workflows/{_morningSlug.Value}/runs?status=done", human));
            default:
                return await Ask(host, Get($"/sindri/workflows/{_morningSlug.Value}/runs", human));
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

    private static GraphSpec ScheduleSpec(string name) =>
        Spec(name) with { Nodes = [new GraphNode(NodeId.From("start"), "schedule", Title: null, new JsonObject { ["cron"] = "0 7 * * 1-5", ["timezone"] = "UTC" }), Spec(name).Nodes[1]] };

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

    // ThreeRuns, then run 4 cancelled at 00:04 and run 5 failed with its start set back to 00:00:30, before the filtered golden's window.
    private static async Task FiveRuns(GoldenHost host, CancellationToken ct)
    {
        await ThreeRuns(host, ct);
        var runs = host.App.Services.GetRequiredService<IWorkflowRunStore>();
        _ = (await runs.Insert(Run(host, 4, WorkflowRunStatus.Cancelled, "manual", "cancelled by owner"), ct)).Value;
        _ = (await runs.Insert(Run(host, 5, WorkflowRunStatus.Failed, "schedule", "node 'agent' timed out") with { StartedAt = host.Now().AddSeconds(30) }, ct)).Value;
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
