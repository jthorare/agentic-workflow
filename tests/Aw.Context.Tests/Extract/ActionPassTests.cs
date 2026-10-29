using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Extract;

public sealed class ActionPassTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");

    private const string ShareDialog =
        "export function ShareDialog() {\n  return <form onSubmit={onRevokeLink}><button onClick={handleCreateLink}>Create</button></form>;\n}\n";

    private const string LinkRoutes =
        "app.post(\"/links\", create);\nrouter.delete(\"/links/:id\", remove);\napp.get(\"/links\", list);\n";

    private static string[] Actions(ExtractedMap map) => [.. map.Nodes.Where(n => n.Key.Kind == NodeKind.Action).Select(n => $"{n.Key.Value}|{n.Label}")];

    private static string[] OnEdges(ExtractedMap map) => [.. map.Edges.Where(e => e.Type == EdgeType.On).Select(e => $"{e.From.Value}>{e.To.Value}").Order(StringComparer.Ordinal)];

    [Fact, Trait("Lane", "unit")]
    public void A_named_click_handler_in_a_component_is_an_action_on_that_component()
    {
        var map = MapExtractor.Extract(_repo, [new("src/ui/ShareDialog.tsx", ShareDialog)]);

        Assert.Equal(["action:ShareDialog.create-link|Create link", "action:ShareDialog.revoke-link|Revoke link"], Actions(map).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["action:ShareDialog.create-link>surface:dialog/ShareDialog", "action:ShareDialog.revoke-link>surface:dialog/ShareDialog"], OnEdges(map), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_inline_arrow_handler_makes_no_action()
    {
        var map = MapExtractor.Extract(_repo, [new("src/ui/ShareDialog.tsx", "export function ShareDialog() {\n  return <button onClick={() => createLink()}>Create</button>;\n}\n")]);

        Assert.Empty(Actions(map));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_changing_route_is_an_action_on_its_route_surface()
    {
        var map = MapExtractor.Extract(_repo, [new("src/api/links.ts", LinkRoutes)]);

        Assert.Equal(["action:route/DELETE:/links/:id|Delete link", "action:route/POST:/links|Create link"], Actions(map).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["action:route/DELETE:/links/:id>surface:route/DELETE:/links/:id", "action:route/POST:/links>surface:route/POST:/links"], OnEdges(map), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_minimal_api_post_is_an_action_on_its_route_surface()
    {
        var map = MapExtractor.Extract(_repo, [new("src/Api/LinkEndpoints.cs", "group.MapPost(\"/links\", Create);\ngroup.MapGet(\"/links\", List);\n")]);

        Assert.Equal(["action:route/POST:/links|Create link"], Actions(map), StringComparer.Ordinal);
        Assert.Equal(["action:route/POST:/links>surface:route/POST:/links"], OnEdges(map), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_titled_command_is_an_action_on_its_command_surface()
    {
        var map = MapExtractor.Extract(_repo, [new("src/commands/registry.ts", "register({ id: \"split-vertical\", title: \"Split vertically\" });")]);

        Assert.Equal(["action:command/split-vertical|Split vertically"], Actions(map), StringComparer.Ordinal);
        Assert.Equal(["action:command/split-vertical>surface:command/split-vertical"], OnEdges(map), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_action_is_a_deterministic_candidate_with_an_implements_edge_from_its_file()
    {
        var map = MapExtractor.Extract(_repo, [new("src/ui/ShareDialog.tsx", ShareDialog), new("src/api/links.ts", LinkRoutes)]);

        var actions = map.Nodes.Where(n => n.Key.Kind == NodeKind.Action).ToList();
        Assert.Equal(4, actions.Count);
        Assert.All(actions, a =>
        {
            Assert.Equal(Provenance.Deterministic, a.Provenance);
            Assert.Equal(0.8, a.Confidence);
            var file = a.Key.Value.StartsWith("action:route/", StringComparison.Ordinal) ? "src/api/links.ts" : "src/ui/ShareDialog.tsx";
            Assert.Equal(["repo:aw-split-panes/" + file], a.EvidenceOrEmpty.Select(e => e.Key.Value), StringComparer.Ordinal);
            var edge = Assert.Single(map.Edges, e => e.Type == EdgeType.Implements && e.To.Value == a.Key.Value);
            Assert.Equal("code:" + file, edge.From.Value);
            Assert.Equal(Provenance.Deterministic, edge.Provenance);
        });
    }

    [Fact, Trait("Lane", "unit")]
    public void An_action_writes_what_its_file_writes()
    {
        var map = MapExtractor.Extract(_repo,
        [
            new("db/schema/001_core.sql", "CREATE TABLE audit_event (id integer);\n"),
            new("src/api/links.ts", LinkRoutes + "export const audit = (db: Db) => db.run(\"INSERT INTO audit_event (id) VALUES (?)\");\n"),
        ]);

        Assert.Contains(map.Edges, e => e is { Type: EdgeType.Writes } && e.From.Value == "code:src/api/links.ts" && e.To.Value == "thing:audit-event");
        Assert.Equal(
            ["action:route/DELETE:/links/:id>thing:audit-event", "action:route/POST:/links>thing:audit-event"],
            map.Edges.Where(e => e is { Type: EdgeType.Writes } && e.From.Kind == NodeKind.Action).Select(e => $"{e.From.Value}>{e.To.Value}").Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }
}
