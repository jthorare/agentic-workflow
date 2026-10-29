using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ShowNode;

public sealed class ShowNodeTests : IDisposable
{
    private const string Orientation = "feature:layout.split-pane.orientation";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task ShowNode_by_alias_gives_the_node_its_aliases_and_its_edges_both_ways()
    {
        _ = await _world.ImportFixture();

        var view = (await _world.Engine.ShowNode("feature:layout.orientation", MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal(("v1", Orientation, "Split orientation"), (view.MapVersion, view.Node.Key.Value, view.Node.Label));
        Assert.Equal((Provenance.Human, true, Trust.Trusted), (view.Node.Provenance, view.Node.Confirmed, view.Node.Trust));
        Assert.Equal(["feature:layout.orientation"], view.Aliases.Select(a => a.Value), StringComparer.Ordinal);
        Assert.Equal([("part-of", "feature:layout.split-pane")], view.Out.Select(e => (EdgeRules.Name(e.Type), e.To.Value)));
        Assert.Equal(["code:src/layout/SplitPane.ts", "surface:command/toggle-orientation"], view.In.Select(e => e.From.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ShowNode_names_the_repo_of_a_node_in_a_multi_repo_map_and_none_for_a_feature_of_several()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:web\", label: \"Web\"}", "  - {key: \"feature:shared\", label: \"Shared\"}",
            "  - {key: \"code:w.ts\", label: \"w.ts\"}", "  - {key: \"code:a.ts\", label: \"a.ts\"}",
            "edges:",
            "  - {from: \"code:w.ts\", to: \"feature:web\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/w.ts\"}]}",
            "  - {from: \"code:w.ts\", to: \"feature:shared\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/w.ts\"}]}",
            "  - {from: \"code:a.ts\", to: \"feature:shared\", type: implements, provenance: deterministic, evidence: [{key: \"repo:other/a.ts\"}]}"));

        var file = (await _world.Engine.ShowNode("code:a.ts", MapWorld.Human, MapWorld.None)).Value;
        var web = (await _world.Engine.ShowNode("feature:web", MapWorld.Human, MapWorld.None)).Value;
        var shared = (await _world.Engine.ShowNode("feature:shared", MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal(("other", "aw-split-panes", null), (file.Node.Repo, web.Node.Repo, shared.Node.Repo));
        Assert.Equal("other", file.Node.ToFact().Repo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Overrides_apply_a_renamed_node_shows_its_new_label_and_a_rejected_one_is_unknown()
    {
        _ = await _world.ImportFixture();

        var renamed = await _world.Engine.ShowNode("feature:tabs.tab-manager", MapWorld.Human, MapWorld.None);
        var rejected = await _world.Engine.ShowNode("feature:layout.drag-to-split", MapWorld.Human, MapWorld.None);

        Assert.Equal("Tabs", renamed.Value.Node.Label);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, rejected.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_label_is_scrubbed_and_an_unaccepted_model_label_is_fenced()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1",
            "kind: authored",
            "repo: aw-split-panes",
            "nodes:",
            "  - {key: \"feature:a\", label: \"A " + FakeScrubber.Secret + " <!-- hidden -->\"}",
            "  - {key: \"feature:b\", label: \"Model said so\", provenance: model, confidence: 0.5}"));

        var a = (await _world.Engine.ShowNode("feature:a", MapWorld.HumanVia(ContextClient.Mcp), MapWorld.None)).Value.Node;
        var b = (await _world.Engine.ShowNode("feature:b", MapWorld.HumanVia(ContextClient.Mcp), MapWorld.None)).Value.Node;

        Assert.Equal("A [REDACTED:fake]", a.Label);
        Assert.Equal(("<untrusted source=\"map\">Model said so</untrusted>", false, Trust.Untrusted), (b.Label, b.Confirmed, b.Trust));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_human_in_the_app_or_the_cli_reads_the_model_label_clean_beside_its_trust_and_confidence()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:b\", label: \"Model said so\", provenance: model, confidence: 0.5}"));

        var app = (await _world.Engine.ShowNode("feature:b", MapWorld.HumanVia(ContextClient.Http), MapWorld.None)).Value.Node;
        var cli = (await _world.Engine.ShowNode("feature:b", MapWorld.Human, MapWorld.None)).Value.Node;

        Assert.Equal(("Model said so", Trust.Untrusted, 0.5), (app.Label, app.Trust, app.Confidence));
        Assert.Equal(("Model said so", Trust.Untrusted, 0.5), (cli.Label, cli.Trust, cli.Confidence));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_map_it_is_SND_CONTEXT_030()
    {
        var view = await _world.Engine.ShowNode(Orientation, MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.NoMap.Code, view.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("aw-split-panes", true, Orientation, null)]
    [InlineData("aw-split-panes", true, "thing:pane-layout", null)]   // in scope through the reads/writes hop (spec §3 "and their cascade"), no longer dropped by the repo cut
    [InlineData("other", true, Orientation, "SND-CONTEXT-003")]
    [InlineData("aw-split-panes", false, Orientation, "SND-CONTEXT-020")]
    public async Task A_session_reaches_only_the_nodes_its_repo_implements(string repo, bool enabled, string key, string? code)
    {
        _ = await _world.ImportFixture();

        var view = await _world.Engine.ShowNode(key, MapWorld.Session(repo, enabled), MapWorld.None);

        Assert.Equal(code, view.IsError ? view.Error.Code : null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_label_two_nodes_share_is_SND_CONTEXT_037()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1",
            "kind: authored",
            "repo: aw-split-panes",
            "nodes:",
            "  - {key: \"feature:a\", label: \"Same\"}",
            "  - {key: \"feature:b\", label: \"Same\"}"));

        var view = await _world.Engine.ShowNode("Same", MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.AmbiguousAnchor.Code, view.Error.Code);
    }

    private const string Ticket = "linear:issue/0e5a1b00-0000-4000-8000-000000000a01";
    private const string Quoted = "linear:issue/0e5a1b00-0000-4000-8000-000000000a02";
    private const string ShortQuote = "keep the pane in view";
    private static readonly string _longQuote = string.Join(' ', Enumerable.Range(1, 80).Select(i => string.Create(CultureInfo.InvariantCulture, $"w{i:00}")));

    private static string DetailMap(string quote) => MapWorld.Yaml(
        "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
        "  - {key: \"feature:a\", label: \"Alpha\", evidence: [{key: \"repo:aw-split-panes/docs/a.md\"}]}",
        "  - {key: \"feature:b\", label: \"Model said so\", provenance: model, confidence: 0.5}",
        "  - {key: \"feature:plain\", label: \"Plain\"}",
        "  - {key: \"code:src/a.ts\", label: \"a.ts\", provenance: deterministic}",
        $"  - {{key: \"record:{Ticket}\", label: \"Ticket title\"}}",
        $"  - {{key: \"record:{Quoted}\", label: \"Quoted ticket\"}}",
        "edges:",
        "  - {from: \"code:src/a.ts\", to: \"feature:a\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/a.ts\"}]}",
        "  - {from: \"feature:b\", to: \"feature:a\", type: part-of, provenance: model, confidence: 0.5}",
        $"  - {{from: \"record:{Ticket}\", to: \"feature:a\", type: mentions}}",
        $"  - {{from: \"{DecisionRow.KeyFor(RecordKey.From(Quoted), quote).Value}\", to: \"feature:a\", type: decides}}",
        "decisions:",
        $"  - {{record: \"{Quoted}\", quote: \"{quote}\", wording: \"Decided.\", at: 2026-07-15T10:00:00Z}}");

    private async Task<NodeView> Show(string key, ContextScope? scope = null) => (await _world.Engine.ShowNode(key, scope ?? MapWorld.Human, MapWorld.None)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_node_names_its_ticket_and_has_no_description()
    {
        _ = await _world.ImportYaml(DetailMap(ShortQuote));

        var node = (await Show("record:" + Ticket)).Node;

        Assert.Equal(new NodeSource("ticket", Ticket), node.Source);
        Assert.Null(node.Description);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_code_node_names_its_file()
    {
        _ = await _world.ImportYaml(DetailMap(ShortQuote));

        var node = (await Show("code:src/a.ts")).Node;

        Assert.Equal(new NodeSource("file", "src/a.ts"), node.Source);
        Assert.Null(node.Description);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_names_the_record_it_quotes_with_its_title_and_describes_itself_with_the_quote()
    {
        _ = await _world.ImportYaml(DetailMap(ShortQuote));

        var node = (await Show(DecisionRow.KeyFor(RecordKey.From(Quoted), ShortQuote).Value)).Node;

        Assert.Equal(new NodeSource("ticket", Quoted, "Quoted ticket"), node.Source);
        Assert.Equal(ShortQuote, node.Description);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_quote_and_the_quoted_title_are_fenced_for_a_caller_that_gets_fences()
    {
        _ = await _world.ImportYaml(DetailMap(ShortQuote));

        var node = (await Show(DecisionRow.KeyFor(RecordKey.From(Quoted), ShortQuote).Value, MapWorld.HumanVia(ContextClient.Mcp))).Node;

        Assert.Equal("<untrusted source=\"map\">Quoted ticket</untrusted>", node.Source!.Title);
        Assert.Equal($"<untrusted source=\"map\">{ShortQuote}</untrusted>", node.Description);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_long_quote_is_cut_on_a_word_at_280_characters_with_an_ellipsis()
    {
        _ = await _world.ImportYaml(DetailMap(_longQuote));

        var description = (await Show(DecisionRow.KeyFor(RecordKey.From(Quoted), _longQuote).Value)).Node.Description!;

        var body = description[..^1];
        Assert.True(description.Length <= 280);
        Assert.EndsWith("…", description, StringComparison.Ordinal);
        Assert.True(description.Length > 250);
        Assert.StartsWith(body, _longQuote, StringComparison.Ordinal);
        Assert.Equal(' ', _longQuote[body.Length]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_node_with_evidence_names_the_first_record_and_one_without_has_no_source()
    {
        _ = await _world.ImportYaml(DetailMap(ShortQuote));

        var withEvidence = (await Show("feature:a")).Node;
        var without = (await Show("feature:plain")).Node;

        Assert.Equal(new NodeSource("doc", "repo:aw-split-panes/docs/a.md"), withEvidence.Source);
        Assert.Null(withEvidence.Description);
        Assert.Null(without.Source);
        Assert.Null(without.Description);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_edge_carries_the_kind_and_label_of_the_node_at_its_other_end()
    {
        _ = await _world.ImportYaml(DetailMap(ShortQuote));

        var center = await Show("feature:a");
        var model = await Show("feature:b");

        Assert.Equal(
            [("code:src/a.ts", NodeKind.Code, "a.ts"), ("feature:b", NodeKind.Feature, "Model said so"), ("record:" + Ticket, NodeKind.Record, "Ticket title")],
            center.In.Where(e => e.Type != EdgeType.Decides).Select(e => (e.From.Value, e.OtherKind, e.OtherLabel)));
        Assert.Contains(center.In, e => e is { Type: EdgeType.Decides, OtherKind: NodeKind.Decision } && string.Equals(e.OtherLabel, "Decided.", StringComparison.Ordinal));
        Assert.Equal([("feature:a", NodeKind.Feature, "Alpha")], model.Out.Select(e => (e.To.Value, e.OtherKind, e.OtherLabel)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_other_end_s_label_is_fenced_like_any_label_when_it_is_untrusted()
    {
        _ = await _world.ImportYaml(DetailMap(ShortQuote));

        var center = await Show("feature:a", MapWorld.HumanVia(ContextClient.Mcp));

        Assert.Equal("<untrusted source=\"map\">Model said so</untrusted>", center.In.Single(e => e.From.Value == "feature:b").OtherLabel);
        Assert.Equal("a.ts", center.In.Single(e => e.From.Value == "code:src/a.ts").OtherLabel);
    }
}
