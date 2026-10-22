using Aw.Context.Core.Code;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Symbols;
using Aw.Context.Core;

namespace Aw.Context.Tests.Code;

public sealed class CodeLensBuilderTests
{
    private static CodeView Build(params string[] anchors) => BuildOn(CodeGraphs.Sharing(), anchors);

    private static CodeView BuildOn(GraphSnapshot graph, params string[] anchors) =>
        CodeLensBuilder.Build(CodeGraphs.Basis, graph, anchors.Length == 0 ? ["feature:sharing"] : anchors).Value;

    [Fact, Trait("Lane", "unit")]
    public void The_envelope_names_the_map_the_anchors_and_when()
    {
        var view = Build();

        Assert.Equal((1, "v2@a41b07d", false, CodeGraphs.AsOf), (view.SchemaVersion, view.MapVersion, view.Draft, view.AsOf));
        var anchor = Assert.Single(view.Anchors);
        Assert.Equal(("feature:sharing", "Sharing", NodeKind.Feature, Trust.Trusted), (anchor.Key.Value, anchor.Label, anchor.Kind, anchor.Trust));
        Assert.Empty(view.MissingAnchors);
        Assert.Empty(view.Lexical);
    }

    [Fact, Trait("Lane", "unit")]
    public void Files_are_the_implementing_files_of_the_own_scope()
    {
        var view = Build();

        Assert.Equal(["src/sharing/links.ts", "src/sharing/routes.ts"], view.Files.Select(f => f.Path), StringComparer.Ordinal);
        var links = view.Files[0];
        Assert.Equal((NodeKind.Code, Provenance.Human, true, false, Trust.Trusted), (links.Kind, links.Provenance, links.Confirmed, links.Suggested, links.Trust));
        Assert.Equal(["feature:sharing"], links.Implements.Select(k => k.Value), StringComparer.Ordinal);
        Assert.Null(links.LinkId);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_model_suggested_file_is_suggested_with_its_link_id_and_untrusted()
    {
        var graph = CodeGraphs.Sharing(
            [CodeGraphs.Node("code:src/sharing/guess.ts", "src/sharing/guess.ts", Provenance.Model)],
            [CodeGraphs.Edge("code:src/sharing/guess.ts", "feature:sharing", EdgeType.Implements, Provenance.Model, 0.6)]);

        var guess = BuildOn(graph).Files.Single(f => f.Path == "src/sharing/guess.ts");

        Assert.Equal((false, true, Trust.Untrusted), (guess.Confirmed, guess.Suggested, guess.Trust));
        Assert.True(guess.LinkId.HasValue);
    }

    [Fact, Trait("Lane", "unit")]
    public void Edge_tests_are_listed_with_basis_edge_and_name_the_files_they_cover()
    {
        var test = Assert.Single(Build().Tests);

        Assert.Equal(("tests/sharing.test.ts", CodeBasis.Edge, true, false), (test.Path, test.Basis, test.Confirmed, test.Suggested));
        Assert.Equal(["feature:sharing"], test.Tests.Select(k => k.Value), StringComparer.Ordinal);
        Assert.Equal(["src/sharing/links.ts", "src/sharing/routes.ts"], test.Named, StringComparer.Ordinal);
        Assert.All(Build().Files, f => Assert.Equal(["tests/sharing.test.ts"], f.TestedBy, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Things_are_own_scope_reads_and_writes_with_access_and_via()
    {
        var things = Build().Data.Things;

        Assert.Equal(["thing:ab", "thing:note", "thing:shared-link"], things.Select(t => t.Key.Value), StringComparer.Ordinal);
        var note = things.Single(t => t.Key.Value == "thing:note");
        Assert.Equal((DataAccess.Reads, NodeKind.Thing, "Note"), (note.Access, note.Kind, note.Label));
        Assert.Equal(["surface:screen/share-dialog"], note.Via.Select(k => k.Value), StringComparer.Ordinal);
        Assert.Equal(DataAccess.Writes, things.Single(t => t.Key.Value == "thing:ab").Access);
    }

    [Fact, Trait("Lane", "unit")]
    public void Thing_read_and_written_is_access_both()
    {
        var link = Build().Data.Things.Single(t => t.Key.Value == "thing:shared-link");

        Assert.Equal(DataAccess.Both, link.Access);
        Assert.Equal(["action:sharing.create-link", "action:sharing.resolve-link"], link.Via.Select(k => k.Value), StringComparer.Ordinal);
        Assert.Equal(["feature:sharing"], link.Anchors.Select(k => k.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_thing_outside_the_own_scope_is_not_listed()
    {
        var graph = CodeGraphs.Sharing(edges: [CodeGraphs.Edge("feature:other", "thing:note", EdgeType.Reads)]);

        Assert.DoesNotContain(BuildOn(graph, "feature:other").Data.Things, t => t.Key.Value == "thing:shared-link");
        Assert.Equal(["thing:note"], BuildOn(graph, "feature:other").Data.Things.Select(t => t.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_thing_is_searchable_when_some_form_of_its_names_has_4_characters()
    {
        var things = Build().Data.Things;

        Assert.True(things.Single(t => t.Key.Value == "thing:shared-link").Searchable);
        Assert.False(things.Single(t => t.Key.Value == "thing:ab").Searchable);
    }

    [Fact, Trait("Lane", "unit")]
    public void Thing_terms_include_the_key_the_label_and_the_aliases()
    {
        var terms = CodeLensBuilder.ThingTerms(CodeGraphs.Sharing(), NodeKey.From("thing:shared-link"));

        Assert.Contains("shared-link", terms, StringComparer.Ordinal);
        Assert.Contains("shared link", terms, StringComparer.Ordinal);
        Assert.Contains("short_url", terms, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Callers_and_callees_are_unavailable_index_not_built()
    {
        var view = Build();

        Assert.All([view.Callers, view.Callees], s => Assert.Equal((SymbolsState.Unavailable, "index-not-built"), (s.State, s.Reason)));
        Assert.All([view.Callers, view.Callees], s => Assert.Empty(s.Items));
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_anchors_merge_one_entry_with_anchors_listed()
    {
        var view = Build("feature:sharing", "surface:screen/share-dialog");

        Assert.Equal(["feature:sharing", "surface:screen/share-dialog"], view.Anchors.Select(a => a.Key.Value), StringComparer.Ordinal);
        var routes = Assert.Single(view.Files, f => f.Path == "src/sharing/routes.ts");
        Assert.Equal(["feature:sharing", "surface:screen/share-dialog"], routes.Anchors.Select(k => k.Value), StringComparer.Ordinal);
        Assert.Equal(["feature:sharing"], Assert.Single(view.Files, f => f.Path == "src/sharing/links.ts").Anchors.Select(k => k.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_name_that_does_not_resolve_is_missing_and_the_rest_are_answered()
    {
        var view = Build("feature:sharing", "feature:nope");

        Assert.Equal(["feature:nope"], view.MissingAnchors, StringComparer.Ordinal);
        _ = Assert.Single(view.Anchors);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_resolvable_anchor_is_SND_CONTEXT_003()
    {
        var result = CodeLensBuilder.Build(CodeGraphs.Basis, CodeGraphs.Sharing(), ["feature:nope"]);

        Assert.Equal(ContextErrors.UnknownAnchor.Code, result.Error.Code);
    }

    private static string Gen(int i, string prefix) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{prefix}src/gen/f{i:D3}.ts");

    [Fact, Trait("Lane", "unit")]
    public void Lists_cap_at_100_and_say_truncated_with_the_full_counts()
    {
        var nodes = Enumerable.Range(0, 101).Select(i => CodeGraphs.Node(Gen(i, "code:"), Gen(i, string.Empty))).ToList();
        var edges = nodes.Select(n => CodeGraphs.Edge(n.Key.Value, "feature:sharing", EdgeType.Implements)).ToList();

        var view = BuildOn(CodeGraphs.Sharing(nodes, edges));

        Assert.Equal((100, true, 103), (view.Files.Count, view.Truncated.Files, view.Counts.Files));
        Assert.False(view.Truncated.Tests);
    }

    [Fact, Trait("Lane", "unit")]
    public void Files_without_a_test_are_counted()
    {
        Assert.Equal(0, Build().Counts.FilesWithoutTest);

        var view = Build("feature:sharing", "feature:other");

        Assert.Equal((3, 1, 1), (view.Counts.Files, view.Counts.FilesWithoutTest, view.Counts.Tests));
        Assert.Empty(view.Files.Single(f => f.Path == "src/other/x.ts").TestedBy);
    }

    [Fact, Trait("Lane", "unit")]
    public void Edges_are_the_stored_links_between_the_found_items_and_the_scope()
    {
        var edges = Build().Edges;

        Assert.Contains(edges, e => e is { Type: EdgeType.Implements } && e.From.Value == "code:src/sharing/links.ts" && e.To.Value == "feature:sharing" && e.Confirmed);
        Assert.Contains(edges, e => e is { Type: EdgeType.Tests } && e.From.Value == "code:tests/sharing.test.ts");
        Assert.Contains(edges, e => e is { Type: EdgeType.Writes } && e.To.Value == "thing:shared-link");
        Assert.Contains(edges, e => e is { Type: EdgeType.Reads } && e.To.Value == "thing:note");
        Assert.DoesNotContain(edges, e => e.From.Value == "code:src/other/x.ts");
        Assert.All(edges, e => Assert.NotEqual(EdgeType.PartOf, e.Type));
    }
}
