using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.ReviewMap;

namespace Aw.Context.Tests.GetLenses;

public sealed class GetLensesTests : IDisposable
{
    private const string Anchor = "feature:tabs.tab-manager";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private async Task ImportTabs() =>
        _ = await _world.ImportYaml(MapWorld.TabsMap);

    private ValueTask<Kernel.Result<LensesView>> Ask(string anchor, ContextScope? scope = null, bool draft = false, int? depth = null) =>
        _world.Engine.Lenses(anchor, draft ? MapRead.NewestDraft : MapRead.Current, depth, scope ?? MapWorld.Human, MapWorld.None);

    private async Task ImportFencedCase() =>
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            "  - {key: \"feature:a\", label: \"A\"}",
            "  - {key: \"feature:b\", label: \"Drafted B\", provenance: model}",
            "edges:",
            "  - {from: \"feature:b\", to: \"feature:a\", type: part-of}"));

    [Fact, Trait("Lane", "unit")]
    public async Task The_tabs_map_shows_three_bands_one_conflict_and_the_suggested_file_link()
    {
        await ImportTabs();

        var view = (await Ask(Anchor)).Value;

        Assert.Equal((false, "v1", Anchor), (view.Draft, view.MapVersion, view.Anchor.Key.Value));
        Assert.Equal((1, 2), (view.Bands.Intent.Counts.Records, view.Bands.Intent.Counts.Decisions));
        Assert.Equal(ConflictKind.Supersedes, Assert.Single(view.Conflicts).Kind);
        var suggested = Assert.Single(view.Bands.Code.Files, f => !f.Confirmed);
        Assert.Equal(("src/tabs/TabGroups.ts", 0.74, Provenance.Model), (suggested.Path, suggested.Confidence, suggested.Provenance));
        Assert.True(suggested.LinkId.HasValue);
        Assert.Equal(view.Bands.Product.Counts.Own, view.Coverage.Scope);
        Assert.Equal(_world.Clock.GetUtcNow(), view.AsOf);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_depth_defaults_to_the_profile_a_deep_one_is_read_up_to_8_and_a_negative_one_is_006()
    {
        await ImportTabs();

        var defaulted = (await Ask(Anchor)).Value;
        var zero = (await Ask(Anchor, depth: 0)).Value;
        var tooDeep = await Ask(Anchor, depth: 5);
        var negative = await Ask(Anchor, depth: -1);

        Assert.Equal(2, defaulted.Depth);
        Assert.Equal(0, zero.Depth);
        Assert.True(tooDeep.IsSuccess);   // the old cap of 4 was raised to 8 and clamps (owner q93, 2026-10-23)
        Assert.Equal(ContextErrors.QueryInvalid.Code, negative.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_negative_lenses_depth_is_006_before_any_map_is_read()
    {
        var none = await Ask(Anchor, depth: -2);

        Assert.Equal(ContextErrors.QueryInvalid.Code, none.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_scope_the_coverage_counts_agrees_with_the_nodes_of_the_map()
    {
        await ImportTabs();

        var view = (await Ask(Anchor)).Value;
        var status = (await _world.Map.MapStatus(anchor: null, MapWorld.Human, MapWorld.None)).Value;

        Assert.True(view.Coverage.Scope <= status.Counts.Nodes);
        Assert.Equal(1, status.Version.Number);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_app_caller_reads_a_clean_label_with_trust_untrusted()
    {
        await ImportFencedCase();

        var view = (await Ask("feature:a", MapWorld.HumanVia(ContextClient.Http))).Value;

        var model = view.Bands.Product.Own.Single(i => i.Node.Value == "feature:b");
        Assert.Equal(("Drafted B", Trust.Untrusted), (model.Label, model.Trust));
        Assert.Equal(("A", Trust.Trusted), (view.Anchor.Label, view.Anchor.Trust));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ContextClient.Mcp)]
    [InlineData(ContextClient.Service)]
    public async Task A_caller_that_is_not_the_cli_or_the_app_reads_the_fenced_label(ContextClient client)
    {
        await ImportFencedCase();

        var view = (await Ask("feature:a", MapWorld.HumanVia(client))).Value;

        var model = view.Bands.Product.Own.Single(i => i.Node.Value == "feature:b");
        Assert.Equal(Trust.Untrusted, model.Trust);
        Assert.NotEqual("Drafted B", model.Label, StringComparer.Ordinal);
        Assert.Contains("Drafted B", model.Label, StringComparison.Ordinal);
        Assert.StartsWith("<untrusted", model.Label, StringComparison.Ordinal);
        Assert.Equal("A", view.Bands.Product.Own.Single(i => i.Node.Value == "feature:a").Label);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fenced_caller_reads_a_records_label_and_a_decisions_quote_fenced_where_the_app_reads_them_clean()
    {
        await ImportTabs();

        var app = (await Ask(Anchor, MapWorld.HumanVia(ContextClient.Http))).Value.Bands.Intent;
        var fenced = (await Ask(Anchor, MapWorld.HumanVia(ContextClient.Mcp))).Value.Bands.Intent;

        Assert.Equal("Tab manager spec", app.Records[0].Label);
        Assert.Equal("a tab group stays expanded; collapsing is a per-user setting", app.Decisions.Single(d => d.Node.Value.StartsWith("decision:ef89", StringComparison.Ordinal)).Quote!.Text);
        Assert.StartsWith("<untrusted", fenced.Records[0].Label, StringComparison.Ordinal);
        Assert.All(fenced.Decisions, d => Assert.StartsWith("<untrusted", d.Quote!.Text, StringComparison.Ordinal));
        Assert.All(fenced.Decisions, d => Assert.DoesNotContain("<untrusted", d.Text, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_path_is_not_fenced_even_when_its_link_is_a_models_suggestion()
    {
        await ImportTabs();

        var view = (await Ask(Anchor, MapWorld.HumanVia(ContextClient.Mcp))).Value;

        Assert.Equal("src/tabs/TabGroups.ts", view.Bands.Code.Files.Single(f => !f.Confirmed).Path);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Asking_for_the_draft_with_none_is_SND_CONTEXT_044_and_without_a_map_030()
    {
        var none = await Ask(Anchor);
        await ImportTabs();

        var noDraft = await Ask(Anchor, draft: true);

        Assert.Equal((ContextErrors.NoMap.Code, ContextErrors.NoDraftMap.Code), (none.Error.Code, noDraft.Error.Code));
        Assert.Contains("context map propose <file>", noDraft.Error.Message, StringComparison.Ordinal);
        Assert.Contains("context map build", noDraft.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_anchor_is_SND_CONTEXT_003_with_suggestions()
    {
        await ImportTabs();

        var view = await Ask("feature:tabs.no-such-node");

        Assert.Equal(ContextErrors.UnknownAnchor.Code, view.Error.Code);
        Assert.Contains("(fix: try ", view.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_draft_is_read_when_asked_and_its_map_says_so()
    {
        var draft = (await Drafts.Build(_world)).Draft;

        var view = (await Ask("surface:command/split-vertical", Drafts.Scope, draft: true)).Value;

        Assert.Equal((true, Drafts.Short(draft), MapWorld.Head[..7]), (view.Draft, view.MapVersion, view.MapCommit));
        Assert.Contains(view.Bands.Code.Files, f => f.Path == "src/commands/ids.ts" && f.Confirmed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_reads_inside_its_repos_scope_only_and_not_with_sessions_off()
    {
        _ = await _world.ImportFixture();

        var inside = await Ask("feature:layout.split-pane", MapWorld.Session(FixtureMap.Repo));
        var outside = await Ask("feature:layout.split-pane", MapWorld.Session("other"));
        var off = await Ask("feature:layout.split-pane", MapWorld.Session(FixtureMap.Repo, enabled: false));

        Assert.True(inside.IsSuccess);
        Assert.Contains(inside.Value.Bands.Code.Files, f => string.Equals(f.Path, "src/layout/SplitPane.ts", StringComparison.Ordinal));   // why: "that repo's files and code" (spec §3): a session's code band is not empty
        Assert.Equal((ContextErrors.UnknownAnchor.Code, ContextErrors.SessionsDisabled.Code), (outside.Error.Code, off.Error.Code));
    }
}
