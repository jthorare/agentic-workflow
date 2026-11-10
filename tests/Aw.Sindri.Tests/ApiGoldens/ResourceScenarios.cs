using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Infrastructure.Resources;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of 'GET /sindri/resources' and 'POST /sindri/resources/purge' (agent-first PR 14): the list (empty, with a workflow's database and disk, a bad owner) and the purge
/// (removed, nothing to remove, refused while the workflow is active, refused while a run is in flight, bad owner). Each scenario provisions the folders it needs under the host's home
/// and seeds the workflow or run through the host's stores. Every request is the owner's.
/// </summary>
internal static class ResourceScenarios
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("resources", "empty"), ("resources", "listed"), ("resources", "bad-owner"),
        ("resources-purge", "purged"), ("resources-purge", "nothing"), ("resources-purge", "active"), ("resources-purge", "running"), ("resources-purge", "bad-owner"),
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
        ArgumentNullException.ThrowIfNull(host);
        var owner = Human(host.Token.Value);
        switch ((slug, scenario))
        {
            case ("resources", "empty"):
                return await Ask(host, Get("/sindri/resources", owner));
            case ("resources", "listed"):
                await Provision(host, "workflow:morning-check", ct);
                return await Ask(host, Get("/sindri/resources", owner));
            case ("resources", "bad-owner"):
                return await Ask(host, Get("/sindri/resources?owner=mail", owner));
            case ("resources-purge", "purged"):
                await Provision(host, "workflow:morning-check", ct);
                return await Ask(host, Post("/sindri/resources/purge", """{"owner":"workflow:morning-check"}""", owner));
            case ("resources-purge", "nothing"):
                return await Ask(host, Post("/sindri/resources/purge", """{"owner":"toolkit:mail"}""", owner));
            case ("resources-purge", "active"):
                await Provision(host, "workflow:morning-check", ct);
                await CreateWorkflow(host, activate: true, ct);
                return await Ask(host, Post("/sindri/resources/purge", """{"owner":"workflow:morning-check"}""", owner));
            case ("resources-purge", "running"):
                await Provision(host, "workflow:morning-check", ct);
                await CreateWorkflow(host, activate: false, ct);
                await RunningRun(host, ct);
                return await Ask(host, Post("/sindri/resources/purge", """{"owner":"workflow:morning-check"}""", owner));
            default:
                return await Ask(host, Post("/sindri/resources/purge", """{"owner":"../etc"}""", owner));
        }
    }

    private static async Task Provision(GoldenHost host, string owner, CancellationToken ct)
    {
        var root = Path.Combine(host.Home, "resources");
        var parsed = ResourceOwner.Parse(owner).Value;
        var plan = ResourcePlanner.Plan(parsed, new ResourceSpec([new DatabaseResource("state")], [new DiskResource("scratch", "1MiB")], Sandbox: null, []), root).Value;
        _ = new ResourceStore(root).Provision(parsed, plan).Value;
        await File.WriteAllBytesAsync(Path.Combine(root, parsed.Folder, "state.db"), new byte[40], ct);
        await File.WriteAllBytesAsync(Path.Combine(root, parsed.Folder, "scratch", "a.bin"), new byte[300], ct);
    }

    private static async Task CreateWorkflow(GoldenHost host, bool activate, CancellationToken ct)
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

    private static async Task RunningRun(GoldenHost host, CancellationToken ct)
    {
        var run = new WorkflowRun(WorkflowRunId.Of(_slug, 1, 1), _id, 1, "{\"kind\":\"manual\"}", WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, host.Now(), EndedAt: null);
        _ = (await host.App.Services.GetRequiredService<IWorkflowRunStore>().Insert(run, ct)).Value;
    }

    private static async Task<string> Ask(GoldenHost host, HttpRequestMessage request) => (await host.Http.Ask(request)).Body;
}
