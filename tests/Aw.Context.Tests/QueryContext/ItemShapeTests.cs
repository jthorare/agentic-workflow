using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Tests.Map;
using static Aw.Context.Tests.SyncSources.ExtractDecisionsTests;

namespace Aw.Context.Tests.QueryContext;

/// <summary>
/// The item shape (S-5B, spec §8.1, Phase 5): a tracker key or record key anchors the query on the record its sync linked, the nodes it touches
/// and the decisions it holds, read through the map with the intent layer merged in.
/// </summary>
[Trait("Lane", "integration")]
public sealed class ItemShapeTests
{
    private const string R106 = "linear:issue/0e5a1b00-0000-4000-8000-000000000106";
    private const string Orientation = "feature:layout.split-pane.orientation";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ContextQuery Item(string key) => new(Text: null, Item: key);

    private static ResultSection Section(ContextResult result, string name) => result.Sections.Single(s => s.Name == name);

    private static async Task<Rig> Synced()
    {
        var rig = await Rig.Start();
        _ = await rig.Sync(Scope());
        return rig;
    }

    // see #1637: owned records carry no aliases yet, so a tracker key (SPL-103) resolves to nothing; the item tests below name the record by its canonical key.

    [Fact]
    public async Task Item_canonical_record_key_is_accepted()
    {
        await using var rig = await Synced();

        var result = await rig.World.Engine.Query(Item(R103), MapWorld.Human, Ct);

        Assert.Contains("record:" + R103, result.Value.Anchors, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Item_anchors_are_mentions_targets_and_decides_targets_of_its_decisions()
    {
        await using var rig = await Synced();

        var anchors = (await rig.World.Engine.Query(Item(R103), MapWorld.Human, Ct)).Value.Anchors;

        Assert.Equal("record:" + R103, anchors[0]);   // the record first, then what it touches
        Assert.Contains(Toggle, anchors, StringComparer.Ordinal);
        Assert.Contains(Orientation, anchors, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Item_answers_with_record_first_in_records_and_its_decisions_quoted()
    {
        await using var rig = await Synced();

        var result = (await rig.World.Engine.Query(Item(R103), MapWorld.Human, Ct)).Value;

        var decision = Assert.Single(Section(result, "decisions").Items, i => i.Text!.Contains(Q103, StringComparison.Ordinal));
        Assert.Equal((Provenance.Model, false, Trust.Untrusted), (decision.Provenance, decision.Confirmed, decision.Trust));
        var cite = Assert.Single(result.Cites.Values, c => c.Key.Value == R103);
        // see #1637: cite.Aliases is empty until owned records carry their tracker identifiers
        Assert.Equal(CitationState.Unchecked, cite.State);
        Assert.Equal("2026-07-23T09:00:00.0000000Z", cite.Refs[0].Version.Value);
        Assert.Contains(Section(result, "records").Items, i => i.Title?.Contains("Toggle split orientation", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Item_unknown_is_003()
    {
        await using var rig = await Synced();

        var result = await rig.World.Engine.Query(Item("SPL-999"), MapWorld.Human, Ct);

        Assert.Equal(ContextErrors.UnknownAnchor.Code, result.Error.Code);
        // see #1637: suggestedAnchors were the records' tracker identifiers (SPL-103); owned records carry none, so nothing is suggested
    }

    [Fact]
    public async Task Item_unmatched_with_context_sync_configured_says_not_linked_yet_and_gives_the_next_action()
    {
        await using var rig = await Synced();

        var result = await rig.World.Engine.Query(Item("NOPE-1"), MapWorld.Human with { SyncConfigured = true }, Ct);

        Assert.Equal(ContextErrors.UnknownAnchor.Code, result.Error.Code);
        Assert.DoesNotContain("name the context-sync service", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("isn't linked yet or the key is unknown", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("next context-sync slot", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("sindri context sources sync", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("sindri context sources status", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Item_unmatched_without_context_sync_still_says_to_name_the_service()
    {
        await using var rig = await Synced();

        var result = await rig.World.Engine.Query(Item("NOPE-1"), MapWorld.Human, Ct);

        Assert.Contains("name the context-sync service", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Item_for_session_sees_only_records_linked_to_in_scope_nodes()
    {
        await using var rig = await Synced();
        var session = MapWorld.Session("aw-split-panes");

        var linked = await rig.World.Engine.Query(Item(R103), session, Ct);
        var surface = await rig.World.Engine.Query(Item(R104), session, Ct);
        var outside = await rig.World.Engine.Query(Item(R106), session, Ct);

        Assert.Contains("record:" + R103, linked.Value.Anchors, StringComparer.Ordinal);   // it mentions a node this repo implements
        Assert.True(surface.IsSuccess);   // it mentions the tab-switch surface, part of the tab manager this repo implements
        Assert.Equal(ContextErrors.UnknownAnchor.Code, outside.Error.Code);   // it touches nothing in scope: not shown
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_item_is_a_query_error(string item)
    {
        using var world = new MapWorld();
        _ = await world.ImportFixture();

        var result = await world.Engine.Query(Item(item), MapWorld.Human, Ct);

        Assert.Equal(ContextErrors.QueryInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_contradicting_pair_of_decisions_on_a_reached_node_shows_in_conflicts()
    {
        await using var rig = await Synced();   // 103 and 104 decide the toggle command and the model said they contradict

        var result = (await rig.World.Engine.Query(Item(R103), MapWorld.Human, Ct)).Value;

        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal(Toggle, conflict.Node);
        Assert.NotEmpty(conflict.Intent);
    }

    [Fact]
    public async Task Conflicts_cite_markers_that_resolve_in_the_result_and_name_no_behavior()
    {
        await using var rig = await Synced();

        var result = (await rig.World.Engine.Query(Item(R103), MapWorld.Human, Ct)).Value;

        var conflict = Assert.Single(result.Conflicts);
        Assert.All(conflict.Intent, marker => Assert.True(result.Cites.ContainsKey(marker), marker + " is not a citation of the result"));
        Assert.DoesNotContain(conflict.Intent, m => m.StartsWith("decision:", StringComparison.Ordinal));
        Assert.Contains(result.Cites.Single(c => c.Value.Key.Value == R103).Key, conflict.Intent, StringComparer.Ordinal);   // the 103 decision's quote comes from the 103 record
        Assert.Empty(conflict.Behavior);
    }

    [Fact]
    public async Task A_decision_cites_the_record_its_quote_comes_from()
    {
        await using var rig = await Synced();

        var result = (await rig.World.Engine.Query(Item(R103), MapWorld.Human, Ct)).Value;

        var decision = Assert.Single(Section(result, "decisions").Items, i => i.Text!.Contains(Q103, StringComparison.Ordinal));
        var record = result.Cites.Single(c => c.Value.Key.Value == R103).Key;
        Assert.Equal([record], decision.Cites);
    }

    [Fact]
    public async Task A_decision_whose_record_was_not_fetched_keeps_the_map_citation()
    {
        await using var rig = await Synced();

        var result = (await rig.World.Engine.Query(Item(R103), MapWorld.Human, Ct)).Value;

        var decision = Assert.Single(Section(result, "decisions").Items, i => i.Text!.Contains(Q103, StringComparison.Ordinal));
        Assert.All(decision.Cites, marker => Assert.True(result.Cites.ContainsKey(marker)));
    }

    [Fact]
    public async Task While_decisions_are_queued_for_a_linked_record_the_query_degrades_042_for_decisions()
    {
        await using var rig = await Rig.Start();
        _ = await rig.Sync(Scope(labeling: false));
        _ = await rig.Sync(Scope(labeling: false));   // the run after the one that fetched

        var result = (await rig.World.Engine.Query(Item(R103), Scope(labeling: false), Ct)).Value;

        var held = Assert.Single(result.Degraded, d => d.Code == ContextErrors.LabelingUnavailable.Code);
        Assert.Equal("decisions", held.Source.Value);
        Assert.Contains("labeling", held.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_every_decision_extracted_the_query_is_not_degraded_for_decisions()
    {
        await using var rig = await Synced();

        var result = (await rig.World.Engine.Query(Item(R103), MapWorld.Human, Ct)).Value;

        Assert.DoesNotContain(result.Degraded, d => d.Code == ContextErrors.LabelingUnavailable.Code);
    }
}
