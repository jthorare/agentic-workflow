using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.ReviewMap;

namespace Aw.Context.Tests.Map;

/// <summary>A deny glob added to the profile after the map exists keeps its code nodes out of every read before any rebuild (review #220 run 1: security-sentinel-1).</summary>
public sealed class DeniedPathReadTests : IDisposable
{
    private const string Denied = "src/layout/SplitPane.ts";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ContextScope Denying(params string[] globs) =>
        new(Settings: null, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, .. globs] }],
            new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

    [Fact, Trait("Lane", "unit")]
    public async Task A_denied_code_file_leaves_the_lenses_before_any_rebuild()
    {
        _ = await _world.ImportFixture();

        var before = (await _world.Engine.Lenses("feature:layout.split-pane", read: MapRead.Current, depth: null, MapWorld.Human, MapWorld.None)).Value;
        var after = (await _world.Engine.Lenses("feature:layout.split-pane", read: MapRead.Current, depth: null, Denying(Denied), MapWorld.None)).Value;

        Assert.Contains(before.Bands.Code.Files, f => string.Equals(f.Path, Denied, StringComparison.Ordinal));
        Assert.DoesNotContain(after.Bands.Code.Files, f => string.Equals(f.Path, Denied, StringComparison.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("prefix:SplitPane")]
    [InlineData("suffix:Pane.ts")]
    [InlineData("regex:^split.*\\.ts$")]
    [InlineData("**/layout/**")]
    public async Task A_role_entry_added_after_the_map_exists_filters_its_nodes_at_query_time(string entry)
    {
        _ = await _world.ImportFixture();

        var after = (await _world.Engine.Lenses("feature:layout.split-pane", read: MapRead.Current, depth: null, Denying(entry), MapWorld.None)).Value;
        var query = await _world.Engine.Query(new ContextQuery(Text: null, Symbol: Denied), Denying(entry), MapWorld.None);

        Assert.DoesNotContain(after.Bands.Code.Files, f => string.Equals(f.Path, Denied, StringComparison.Ordinal));
        Assert.Equal(ContextErrors.UnknownAnchor.Code, query.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_symbol_query_for_a_denied_file_is_003_like_any_unknown_anchor()
    {
        _ = await _world.ImportFixture();

        var result = await _world.Engine.Query(new ContextQuery(Text: null, Symbol: Denied), Denying(Denied), MapWorld.None);

        Assert.Equal(ContextErrors.UnknownAnchor.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_denied_code_node_is_not_shown_and_a_feature_only_it_implemented_goes_with_it()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:kept\", label: \"Kept\"}",
            "  - {key: \"feature:drafted\", label: \"Drafted\", provenance: model}",
            "  - {key: \"code:private/a.ts\", label: \"a.ts\"}",
            "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
            "edges:",
            "  - {from: \"code:private/a.ts\", to: \"feature:drafted\", type: implements}",
            "  - {from: \"code:private/a.ts\", to: \"feature:kept\", type: implements}",
            "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements}"));

        var shown = await _world.Engine.ShowNode("code:private/a.ts", Denying("private/**"), MapWorld.None);
        var drafted = await _world.Engine.ShowNode("feature:drafted", Denying("private/**"), MapWorld.None);
        var kept = await _world.Engine.ShowNode("feature:kept", Denying("private/**"), MapWorld.None);

        Assert.Equal(ContextErrors.UnknownAnchor.Code, shown.Error.Code);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, drafted.Error.Code);
        Assert.True(kept.IsSuccess);
        var edgeEnds = kept.Value.In.Select(f => f.From.Value).ToList();
        Assert.Contains("code:src/b.ts", edgeEnds, StringComparer.Ordinal);
        Assert.DoesNotContain("code:private/a.ts", edgeEnds, StringComparer.Ordinal);
        var near = (await _world.Engine.Neighbors("feature:kept", depth: 1, Denying("private/**"), MapWorld.None)).Value;
        Assert.DoesNotContain(near.Nodes, n => string.Equals(n.Node.Key.Value, "code:private/a.ts", StringComparison.Ordinal));
    }

    private static readonly string[] _deniedMap =
    [
        "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
        "  - {key: \"feature:kept\", label: \"Kept\"}",
        "  - {key: \"code:private/a.ts\", label: \"a.ts\"}",
        "  - {key: \"code:src/b.ts\", label: \"b.ts\"}",
        "  - {key: \"surface:setting/secret\", label: \"secret\", provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/private/s.json\"}]}",
        "  - {key: \"surface:setting/mixed\", label: \"mixed\", provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/private/s.json\"}, {key: \"repo:aw-split-panes/src/b.ts\"}]}",
        "edges:",
        "  - {from: \"code:private/a.ts\", to: \"feature:kept\", type: implements, evidence: [{key: \"repo:aw-split-panes/private/a.ts\"}]}",
        "  - {from: \"code:src/b.ts\", to: \"feature:kept\", type: implements, evidence: [{key: \"repo:aw-split-panes/src/b.ts\"}]}",
        "  - {from: \"surface:setting/secret\", to: \"feature:kept\", type: part-of, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/private/s.json\"}]}",
        "  - {from: \"surface:setting/mixed\", to: \"feature:kept\", type: part-of, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/private/s.json\"}, {key: \"repo:aw-split-panes/src/b.ts\"}]}",
    ];

    [Fact, Trait("Lane", "unit")]
    public async Task An_export_leaves_out_denied_code_nodes_their_edges_and_surfaces_only_a_denied_file_produced()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(_deniedMap));
        var scope = Denying("private/**");

        var exported = await _world.Map.ExportMap(path: null, scope, MapWorld.None);

        var document = _world.Exports.Documents[exported.Value.Path];
        var keys = document.Nodes.Select(n => n.Key.Value).ToList();
        Assert.DoesNotContain("code:private/a.ts", keys, StringComparer.Ordinal);
        Assert.DoesNotContain("surface:setting/secret", keys, StringComparer.Ordinal);
        Assert.Contains("surface:setting/mixed", keys, StringComparer.Ordinal);
        Assert.DoesNotContain(document.Edges ?? [], e => e.From.Value.Contains("private/", StringComparison.Ordinal) || string.Equals(e.From.Value, "surface:setting/secret", StringComparison.Ordinal));
        var evidence = document.Nodes.SelectMany(n => n.EvidenceOrEmpty).Concat((document.Edges ?? []).SelectMany(e => e.EvidenceOrEmpty)).Select(r => r.Key.Value).ToList();
        Assert.DoesNotContain(evidence, e => e.Contains("private/", StringComparison.Ordinal));
        Assert.Contains("repo:aw-split-panes/src/b.ts", evidence, StringComparer.Ordinal);   // the implements edge keeps its allowed evidence
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Map_status_with_an_anchor_covers_what_the_lenses_cover_once_a_path_is_denied()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(_deniedMap));
        var scope = Denying("private/**");

        var status = (await _world.Map.MapStatus("feature:kept", scope, MapWorld.None)).Value;
        var lenses = (await _world.Engine.Lenses("feature:kept", read: MapRead.Current, depth: null, scope, MapWorld.None)).Value;
        var denied = await _world.Map.MapStatus("code:private/a.ts", scope, MapWorld.None);

        Assert.Equivalent(lenses.Coverage with { Map = null }, status.Coverage);
        Assert.Equal(1, status.Coverage!.Code.Implemented);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, denied.Error.Code);
        Assert.Equal(3, status.Counts.Nodes);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_review_queue_leaves_out_a_denied_file_s_code_nodes()
    {
        _ = await Drafts.Build(_world);
        var deny = new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "src/commands/**"] };
        var scope = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [deny]);

        var plain = await Drafts.Queue(_world);
        var queue = (await _world.Map.ReviewQueue(limit: null, scope, MapWorld.None)).Value;

        Assert.Contains(plain.Entries, e => e.Files.Any(f => f.Value.Contains("src/commands/", StringComparison.Ordinal)) || e.Node.Value.Contains("close-pane", StringComparison.Ordinal));
        Assert.DoesNotContain(queue.Entries, e => e.Files.Any(f => f.Value.Contains("src/commands/", StringComparison.Ordinal)) || e.Node.Value.Contains("close-pane", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_export_keeps_a_person_s_node_without_its_denied_evidence_and_drops_every_override_about_a_denied_path()
    {
        var lines = _deniedMap.ToList();
        lines.Insert(lines.FindIndex(l => string.Equals(l, "edges:", StringComparison.Ordinal)), "  - {key: \"surface:setting/human\", label: \"human\", provenance: human, evidence: [{key: \"repo:aw-split-panes/private/s.json\"}]}");
        _ = await _world.ImportYaml(MapWorld.Yaml([.. lines]));
        var at = DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var orphan = new MapOverride(OverrideAction.Rename, NodeKey.From("code:private/x.ts"), "tester", at, "x");   // a decision about a file the map no longer has
        var onNode = new MapOverride(OverrideAction.Rename, NodeKey.From("code:private/a.ts"), "tester", at, "a");
        var kept = new MapOverride(OverrideAction.Rename, NodeKey.From("code:src/b.ts"), "tester", at, "b");
        _ = await _world.Store.AppendOverrides([orphan, onNode, kept], MapWorld.None);

        var exported = await _world.Map.ExportMap(path: null, Denying("private/**"), MapWorld.None);

        var document = _world.Exports.Documents[exported.Value.Path];
        var human = Assert.Single(document.Nodes, n => n.Key.Value == "surface:setting/human");
        Assert.Empty(human.EvidenceOrEmpty);
        Assert.Equal("code:src/b.ts", Assert.Single(document.Overrides ?? []).Target.Value);
        Assert.Equal(1, exported.Value.Counts.Overrides);
        Assert.DoesNotContain(document.Orphaned ?? [], k => k.Value.Contains("private/", StringComparison.Ordinal));
    }
}
