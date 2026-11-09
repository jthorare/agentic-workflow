using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of the workflow writes (issue 2258, PR 8b): the answers that need no block catalog, so they hold on the golden host: a pause, an archive, the refusals (a stale base revision with the current revision,
/// a body missing a field, an unknown workflow) and a validate with nothing to validate. The graph-checking answers (a create, an ops batch that applies, an activate) are pinned by the service tests.
/// </summary>
internal static class WorkflowWriteScenarios
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");

    private const string Rename = "[{\"op\":\"rename\",\"node\":\"start\",\"to\":\"begin\"}]";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("workflows", "create-nothing-to-create"), ("workflows-validate", "validate-nothing-to-check"),
        ("workflows-slug", "replace-stale"), ("workflows-slug", "archived"), ("workflows-slug", "archive-not-found"),
        ("workflows-slug-ops", "ops-stale"), ("workflows-slug-ops", "ops-missing-fields"),
        ("workflows-slug-pause", "paused"), ("workflows-slug-activate", "not-found"),
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
        var route = $"/sindri/workflows/{_slug.Value}";
        switch ((slug, scenario))
        {
            case ("workflows", "create-nothing-to-create"):
                return await Ask(host, Post("/sindri/workflows", $"{{\"slug\":\"{_slug.Value}\"}}", human));
            case ("workflows-validate", "validate-nothing-to-check"):
                return await Ask(host, Post("/sindri/workflows/validate", "{}", human));
            case ("workflows-slug", "replace-stale"):
                await Seed(host, newerRevision: true, ct);
                return await Ask(host, Put(route, "{\"baseRevision\":1,\"spec\":{\"version\":1,\"name\":\"Morning check\",\"nodes\":[],\"edges\":[]}}", human));
            case ("workflows-slug", "archived"):
                await Seed(host, newerRevision: false, ct);
                return await Ask(host, Delete(route, human));
            case ("workflows-slug", "archive-not-found"):
                return await Ask(host, Delete("/sindri/workflows/nope", human));
            case ("workflows-slug-ops", "ops-stale"):
                await Seed(host, newerRevision: true, ct);
                return await Ask(host, Post(route + "/ops", $"{{\"baseRevision\":1,\"ops\":{Rename}}}", human));
            case ("workflows-slug-ops", "ops-missing-fields"):
                await Seed(host, newerRevision: false, ct);
                return await Ask(host, Post(route + "/ops", "{}", human));
            case ("workflows-slug-pause", "paused"):
                await Seed(host, newerRevision: false, ct);
                return await Ask(host, Post(route + "/pause", json: null, human));
            default:
                return await Ask(host, Post("/sindri/workflows/nope/activate", json: null, human));
        }
    }

    private static async Task Seed(GoldenHost host, bool newerRevision, CancellationToken ct)
    {
        var spec = new GraphSpec(Version: 1, Name: "Morning check", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        var workflows = host.App.Services.GetRequiredService<IWorkflowStore>();
        _ = (await workflows.Create(_id, _slug, spec, "owner", ct)).Value;
        if (newerRevision)
        {
            _ = (await workflows.Save(_id, 1, spec, "session:golden", ct)).Value;
        }
    }

    private static async Task<string> Ask(GoldenHost host, HttpRequestMessage request) => (await host.Http.Ask(request)).Body;
}
