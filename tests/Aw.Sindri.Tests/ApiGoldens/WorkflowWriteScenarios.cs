using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of the workflow writes (issue 2258, PR 8b): a pause, an archive, the refusals (a stale base revision with the current revision, a body missing a field, an unknown workflow), a validate with nothing to validate, and the success answers
/// of a replace, an ops batch and an activate. The golden host runs the real block catalog (schedule, manual, condition, notify, prompt), so the seeded one-node 'manual' workflow validates; the listing of the blocks is pinned here too.
/// A create that validates is pinned by the service tests.
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
        ("workflows-slug-ops", "ops-applied"), ("workflows-slug-activate", "activated"), ("workflows-blocks", "listed"), ("workflows-slug", "replaced"), ("workflows", "created"),
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
    public static Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        var human = Human(host.Token.Value);
        return _bodies.TryGetValue((slug, scenario), out var run)
            ? run(host, human, ct)
            : Ask(host, Post("/sindri/workflows/nope/activate", json: null, human));
    }

    private delegate Task<string> Scenario(GoldenHost host, (string Name, string Value)[] human, CancellationToken ct);

    private static string Route => $"/sindri/workflows/{_slug.Value}";

    private static Scenario Seeded(bool newerRevision, Func<(string Name, string Value)[], HttpRequestMessage> request) =>
        async (host, human, ct) =>
        {
            await Seed(host, newerRevision, ct);
            return await Ask(host, request(human));
        };

    private static Scenario Plain(Func<(string Name, string Value)[], HttpRequestMessage> request) => (host, human, _) => Ask(host, request(human));

    private static readonly Dictionary<(string Slug, string Scenario), Scenario> _bodies = new()
    {
        [("workflows", "create-nothing-to-create")] = Plain(h => Post("/sindri/workflows", $"{{\"slug\":\"{_slug.Value}\"}}", h)),
        [("workflows", "created")] = Plain(h => Post("/sindri/workflows", $"{{\"slug\":\"{_slug.Value}\",\"template\":\"manual-notify\"}}", h)),   // why: the one create that succeeds, so the write sweep sees workflow.created by the launched session
        [("workflows-validate", "validate-nothing-to-check")] = Plain(h => Post("/sindri/workflows/validate", "{}", h)),
        [("workflows-slug", "replace-stale")] = Seeded(newerRevision: true, h => Put(Route, "{\"baseRevision\":1,\"spec\":{\"version\":1,\"name\":\"Morning check\",\"nodes\":[],\"edges\":[]}}", h)),
        [("workflows-slug", "archived")] = Seeded(newerRevision: false, h => Delete(Route, h)),
        [("workflows-slug", "archive-not-found")] = Plain(h => Delete("/sindri/workflows/nope", h)),
        [("workflows-slug-ops", "ops-stale")] = Seeded(newerRevision: true, h => Post(Route + "/ops", $"{{\"baseRevision\":1,\"ops\":{Rename}}}", h)),
        [("workflows-slug-ops", "ops-missing-fields")] = Seeded(newerRevision: false, h => Post(Route + "/ops", "{}", h)),
        [("workflows-slug-pause", "paused")] = Seeded(newerRevision: false, h => Post(Route + "/pause", json: null, h)),
        [("workflows-slug-ops", "ops-applied")] = Seeded(newerRevision: false, h => Post(Route + "/ops", $"{{\"baseRevision\":1,\"ops\":{Rename}}}", h)),
        [("workflows-slug-activate", "activated")] = Seeded(newerRevision: false, h => Post(Route + "/activate", json: null, h)),
        [("workflows-blocks", "listed")] = Plain(h => Get("/sindri/workflows/blocks", h)),
        [("workflows-slug", "replaced")] = Seeded(newerRevision: false, h => Put(Route, "{\"baseRevision\":1,\"spec\":{\"version\":1,\"name\":\"Morning check v2\",\"nodes\":[{\"id\":\"start\",\"type\":\"manual\",\"config\":{}}],\"edges\":[]}}", h)),
    };

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
