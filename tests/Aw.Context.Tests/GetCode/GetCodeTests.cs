using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.GetCode;

public sealed class GetCodeTests : IDisposable
{
    private readonly CodeWorld _code = new();

    public void Dispose() => _code.Dispose();

    private static SearchedSource Row(CodeView view, string id) => Assert.Single(view.Searched.Sources, s => s.Id.Value == id);

    [Fact, Trait("Lane", "unit")]
    public async Task Lexical_tests_are_added_when_no_edge_exists()
    {
        _ = await _code.ImportSharing();

        var view = (await _code.Code()).Value;

        var links = Assert.Single(view.Tests, t => string.Equals(t.Path, "tests/links.test.ts", StringComparison.Ordinal));
        Assert.Equal((CodeBasis.Imports, true, false, null), (links.Basis, links.Suggested, links.Confirmed, links.LinkId));
        Assert.Equal(["src/sharing/links.ts"], links.Named, StringComparer.Ordinal);
        Assert.DoesNotContain(view.Tests, t => string.Equals(t.Path, "tests/other.test.ts", StringComparison.Ordinal));
        Assert.Contains(view.Files.Single(f => string.Equals(f.Path, "src/sharing/links.ts", StringComparison.Ordinal)).TestedBy, t => string.Equals(t, "tests/links.test.ts", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Edge_and_lexical_for_the_same_path_is_one_entry_with_basis_edge()
    {
        _ = await _code.ImportSharing();

        var view = (await _code.Code()).Value;

        var edge = Assert.Single(view.Tests, t => t.Path == "tests/edge.test.ts");
        Assert.Equal((CodeBasis.Edge, true), (edge.Basis, edge.Named.Contains("src/sharing/links.ts", StringComparer.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Imports_beats_sibling_and_a_sibling_alone_is_listed()
    {
        _ = await _code.ImportSharing();

        var view = (await _code.Code()).Value;

        Assert.Equal(CodeBasis.Imports, view.Tests.Single(t => t.Path == "tests/links.test.ts").Basis);
        Assert.Equal(CodeBasis.Sibling, view.Tests.Single(t => t.Path == "tests/routes.test.ts").Basis);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Schema_files_come_only_from_schemaGlobs()
    {
        _ = await _code.ImportSharing();

        var defaults = (await _code.Code()).Value;
        var narrowed = (await _code.Code(MapWorld.HumanWith("{\"code\":{\"schemaGlobs\":[\"db/**\"],\"testGlobs\":" + CodeWorld.TestGlobs + "}}"))).Value;

        var file = Assert.Single(defaults.Data.Files);
        Assert.Equal(("migrations/002_sharing.sql", "thing:shared-link"), (file.Path, file.Thing.Value));
        Assert.Empty(narrowed.Data.Files);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Empty_schemaGlobs_is_not_configured_and_searches_nothing_for_schema_files()
    {
        _ = await _code.ImportSharing();

        var view = (await _code.Code(MapWorld.HumanWith("{\"code\":{\"schemaGlobs\":[],\"testGlobs\":" + CodeWorld.TestGlobs + "}}"))).Value;

        var row = Row(view, "lexical-schema");
        Assert.Equal((SearchedState.NotConfigured, "not-configured"), (row.State, row.Reason));
        Assert.Empty(view.Data.Files);
        Assert.DoesNotContain(_code.Search.Queries, q => q.Terms.Any(t => t.Contains("shared", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Denied_path_is_never_listed_or_asked_for()
    {
        _ = await _code.ImportSharing();
        IReadOnlyList<ContextRepo> repos = [MapWorld.Repos[0] with { DenyPaths = [.. PathDenylist.Defaults, "migrations/002*"] }, MapWorld.Repos[1]];
        using var section = JsonDocument.Parse("{}");
        var scope = new ContextScope(section.RootElement.Clone(), repos, new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

        var view = (await _code.Code(scope)).Value;

        Assert.Empty(view.Data.Files);
        Assert.All(_code.Search.Queries, q => Assert.DoesNotContain("migrations/002_sharing.sql", q.Within ?? [], StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_of_another_repo_gets_no_anchor_and_nothing_is_searched()
    {
        _ = await _code.ImportSharing();

        var view = await _code.Code(MapWorld.Session("other"));

        Assert.Equal(ContextErrors.UnknownAnchor.Code, view.Error.Code);
        Assert.Empty(_code.Search.Queries);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_map_versions_commit_is_the_commit_searched()
    {
        _ = await _code.ImportSharing(commit: MapWorld.Head);

        var view = (await _code.Code()).Value;

        var lexical = Assert.Single(view.Lexical);
        Assert.Equal(("aw-split-panes", MapWorld.Head[..7], 2), (lexical.Repo, lexical.Commit, lexical.SearchedFiles));
        Assert.All(_code.Search.Queries, q => Assert.Equal(MapWorld.Head, q.Commit));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_commit_the_clone_does_not_hold_is_unavailable_with_110_and_the_route_succeeds()
    {
        _ = await _code.ImportSharing(commit: "ffffffffffffffffffffffffffffffffffffffff");

        var view = await _code.Code();

        Assert.True(view.IsSuccess);
        foreach (var id in new[] { "lexical-tests", "lexical-schema" })
        {
            var row = Row(view.Value, id);
            Assert.Equal((SearchedState.Unavailable, "commit-not-held", ContextErrors.CommitUnknown.Code), (row.State, row.Reason, row.Code));
        }

        Assert.Equal(2, view.Value.Files.Count);
        Assert.Empty(view.Value.Data.Files);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_deadline_returns_what_was_found_with_152_as_a_degraded_row()
    {
        using var slow = new CodeWorld(decorateTimed: (inner, expire) => new StallingSearch(inner, _ => true, expire));
        _ = await slow.ImportSharing();

        var view = (await slow.Code(MapWorld.HumanWith("{\"code\":{\"deadline\":\"100ms\",\"testGlobs\":" + CodeWorld.TestGlobs + "}}"))).Value;

        var row = Row(view, "lexical-tests");
        Assert.Equal((SearchedState.Degraded, ContextErrors.CodeSearchTimedOut.Code), (row.State, row.Code));
        Assert.Contains(view.Degraded, d => d.StartsWith("lexical-tests", StringComparison.Ordinal));
        Assert.Equal(2, view.Files.Count);
        Assert.Contains(view.Tests, t => t.Path == "tests/routes.test.ts");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_search_that_runs_past_the_query_deadline_but_within_the_code_deadline_is_not_expired()
    {
        CodeWorld? world = null;
        using var slow = new CodeWorld(decorateTimed: (inner, _) => new AdvancingSearch(inner, () => world!.World.Clock.Advance(TimeSpan.FromSeconds(10))));
        world = slow;
        _ = await slow.ImportSharing();

        var view = (await slow.Code(MapWorld.HumanWith("{\"deadline\":\"4s\",\"code\":{\"testGlobs\":" + CodeWorld.TestGlobs + "}}"))).Value;

        Assert.All(["lexical-tests", "lexical-schema"], id => Assert.NotEqual(ContextErrors.CodeSearchTimedOut.Code, Row(view, id).Code, StringComparer.Ordinal));
        Assert.Contains(view.Tests, t => t.Path == "tests/routes.test.ts");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task More_than_200_implementing_files_is_ok_with_reason_capped()
    {
        var files = new Dictionary<string, string>(CodeWorld.Files, StringComparer.Ordinal);
        var nodes = new List<string>();
        var edges = new List<string>();
        for (var i = 0; i < CodeRules.MaxImplementingFiles + 1; i++)
        {
            files[$"src/gen/f{i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)}.ts"] = "export {};";
            nodes.Add($"  - {{key: \"code:src/gen/f{i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)}.ts\", label: \"f\", provenance: deterministic}}");
            edges.Add($"  - {{from: \"code:src/gen/f{i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)}.ts\", to: \"feature:sharing\", type: implements, provenance: deterministic, evidence: [{{key: \"repo:aw-split-panes/src/gen/f{i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)}.ts\"}}]}}");
        }

        using var big = new CodeWorld(files);
        _ = await big.ImportSharing(extraNodes: [.. nodes], extraEdges: [.. edges]);

        var view = (await big.Code()).Value;

        var row = Row(view, "lexical-tests");
        Assert.Equal((SearchedState.Ok, "capped"), (row.State, row.Reason));
        Assert.True(view.Truncated.SearchedFiles);
        Assert.Equal(CodeRules.MaxImplementingFiles, Assert.Single(view.Lexical).SearchedFiles);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Searched_has_the_map_the_code_index_and_the_two_lexical_rows()
    {
        _ = await _code.ImportSharing();

        var view = (await _code.Code()).Value;

        Assert.Equal(["map", "code-index", "lexical-tests", "lexical-schema"], view.Searched.Sources.Select(s => s.Id.Value), StringComparer.Ordinal);
        Assert.Equal((SearchedState.Unavailable, "index-not-built"), (Row(view, "code-index").State, Row(view, "code-index").Reason));
        Assert.Equal((SearchedState.Ok, SearchedState.Ok), (Row(view, "lexical-tests").State, Row(view, "lexical-schema").State));
        Assert.Equal(0, view.Searched.Depth);
        Assert.Equal([CodeWorld.Anchor], view.Searched.Anchors, StringComparer.Ordinal);
        Assert.False(view.Searched.Bands.Code.Empty);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Coverage_and_searched_are_the_lenses_objects_not_a_second_vocabulary()
    {
        _ = await _code.ImportSharing();

        var code = (await _code.Code()).Value;
        var lenses = (await _code.World.Engine.LensesOf([CodeWorld.Anchor], Context.Core.Map.MapRead.Current, depth: 0, CodeWorld.WithTestGlobs, MapWorld.None)).Value;

        Assert.Equal(JsonSerializer.Serialize(lenses.Coverage), JsonSerializer.Serialize(code.Coverage));
        Assert.NotNull(code.Coverage.Map);
        Assert.Contains(code.Coverage.ByAnchor!.Keys, k => string.Equals(k, CodeWorld.Anchor, StringComparison.Ordinal));
        Assert.Equal(lenses.Searched.Sources.Select(s => s.Id.Value).Where(i => string.Equals(i, "map", StringComparison.Ordinal) || string.Equals(i, "code-index", StringComparison.Ordinal)), code.Searched.Sources.Select(s => s.Id.Value).Where(i => string.Equals(i, "map", StringComparison.Ordinal) || string.Equals(i, "code-index", StringComparison.Ordinal)), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Text_is_scrubbed_and_a_secret_in_a_label_is_never_a_search_term()
    {
        _ = await _code.ImportSharing();
        _ = await _code.World.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:sharing\", label: \"Sharing\"}",
            "  - {key: \"surface:screen/share\", label: \"Share\"}",
            "  - {key: \"action:sharing.create\", label: \"Create\"}",
            "  - {key: \"thing:shared-link\", label: \"Shared ACME-123456 link\"}",
            "  - {key: \"code:src/sharing/links.ts\", label: \"links.ts\", provenance: deterministic}",
            "edges:",
            "  - {from: \"surface:screen/share\", to: \"feature:sharing\", type: part-of}",
            "  - {from: \"action:sharing.create\", to: \"surface:screen/share\", type: on}",
            "  - {from: \"action:sharing.create\", to: \"thing:shared-link\", type: writes}",
            "  - {from: \"code:src/sharing/links.ts\", to: \"feature:sharing\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/sharing/links.ts\"}]}"));
        var scope = MapWorld.HumanWith("""{"scrub":{"extraPatterns":[{"id":"acme-id","pattern":"ACME-[0-9]{6}"}]}}""");

        var view = (await _code.Code(scope)).Value;

        Assert.All(_code.Search.Queries, q => Assert.All(q.Terms, t => Assert.DoesNotContain("123456", t, StringComparison.Ordinal)));
        Assert.DoesNotContain("123456", JsonSerializer.Serialize(view), StringComparison.Ordinal);
        Assert.Contains("[REDACTED:acme-id]", view.Data.Things.Single().Label, StringComparison.Ordinal);
        _ = Assert.Single(view.Data.Files);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_map_version_that_is_not_retained_is_120()
    {
        _ = await _code.ImportSharing();

        var view = await _code.Code(mapVersion: "v99");

        Assert.Equal(ContextErrors.MapVersionNotRetained.Code, view.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Fifty_anchors_are_not_refused_for_their_count_and_a_draft_with_a_version_is_006()
    {
        _ = await _code.ImportSharing();

        var many = await _code.Code(anchors: [.. Enumerable.Range(0, 50).Select(i => $"feature:n{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}")]);
        var both = await _code.Code(draft: true, mapVersion: "v1");

        Assert.Equal((ContextErrors.UnknownAnchor.Code, ContextErrors.QueryInvalid.Code), (many.Error.Code, both.Error.Code));
        Assert.Empty(_code.Search.Queries);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_anchor_is_missing_beside_a_known_one_and_none_known_is_003()
    {
        _ = await _code.ImportSharing();

        var one = (await _code.Code(anchors: [CodeWorld.Anchor, "feature:nope"])).Value;
        var none = await _code.Code(anchors: ["feature:nope"]);

        Assert.Equal(["feature:nope"], one.MissingAnchors, StringComparer.Ordinal);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, none.Error.Code);
    }
}
