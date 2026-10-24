using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The map view model (F3 spec 1): the words on each node, the pills, the band frames and the control row's coverage, read from the map-tabs golden.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextMapViewModelTests
{
    private static readonly HashSet<string> _quiet = new(["part-of", "on"], StringComparer.Ordinal);
    private static readonly HashSet<string> _loud = new(["implements", "decides", "mentions", "conflicts"], StringComparer.Ordinal);
    private static readonly string[] _letters = ["A", "B", "C"];

    private static ContextMapViewModel Map(Action<JsonNode>? edit = null)
    {
        var node = GoldenFiles.Node("context-lenses", "map-tabs");
        edit?.Invoke(node);
        var read = AwJson.Deserialize<LensesView>(Encoding.UTF8.GetBytes(node.ToJsonString()), SindriApiClient.Json);
        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : string.Empty);
        return new ContextMapViewModel(read.Value);
    }

    private static MapNodeViewModel Node(ContextMapViewModel map, string key) => map.Node(key)!;

    [Fact]
    public void Every_node_is_named_by_its_kind_word_and_label_for_assistive_technology()
    {
        var map = Map();

        Assert.Equal("Feature: Tab manager", Node(map, "feature:tabs.tab-manager").Name);
        Assert.Equal("Action: Group the selected tabs", Node(map, "action:tab-groups.group-selected").Name);
        Assert.Equal("File: TabManager.ts", Node(map, "code:src/tabs/TabManager.ts").Name);
        Assert.All(map.Nodes, n => Assert.Contains(": ", n.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void A_caption_is_the_kind_plus_its_technical_detail()
    {
        var map = Map();

        Assert.Equal("Feature · " + Labels.ContextMapAnchorDetail, Node(map, "feature:tabs.tab-manager").Caption);
        Assert.Equal("feature", Node(map, "feature:tabs.tab-groups").Caption);
        Assert.Equal("ticket · linear · " + Labels.ContextUntrusted, map.Nodes.Single(n => n.Layout.Kind == "record").Caption);
        Assert.StartsWith("Decision · Jul ", map.Nodes.First(n => n.Layout.Kind == "decision").Caption, StringComparison.Ordinal);
        Assert.Equal("src/tabs", Node(map, "code:src/tabs/TabManager.ts").Caption);
    }

    [Fact]
    public void A_suggested_node_carries_the_words_never_only_the_dash()
    {
        var map = Map();

        var suggested = Node(map, "code:src/tabs/TabGroups.ts");
        Assert.True(suggested.IsSuggested);
        Assert.False(suggested.IsSolid);
        Assert.Equal("src/tabs · " + Labels.ContextMapSuggestedPct(74) + " · " + Labels.ContextUntrusted, suggested.Caption);
        Assert.True(Node(map, "code:src/tabs/TabManager.ts").IsSolid);
    }

    [Fact]
    public void A_node_nobody_confirmed_is_dashed_whoever_found_it_and_untrusted_text_says_so()
    {
        var map = Map(node =>
        {
            var found = node["bands"]!["product"]!["own"]![1]!;
            found["confirmed"] = false;
            found["provenance"] = "deterministic";
        });

        var action = Node(map, "action:tab-groups.group-selected");
        Assert.True(action.IsSuggested);
        Assert.False(action.IsSolid);
        Assert.DoesNotContain("%", action.Caption, StringComparison.Ordinal);   // the confidence is only for what a model proposed
        Assert.True(Node(map, "feature:tabs.tab-manager").IsSolid);
        Assert.EndsWith(Labels.ContextUntrusted, map.Nodes.Single(n => n.Layout.Kind == "record").Caption, StringComparison.Ordinal);
        Assert.DoesNotContain(Labels.ContextUntrusted, Node(map, "feature:tabs.tab-manager").Caption, StringComparison.Ordinal);
    }

    [Fact]
    public void The_anchor_card_says_it_is_in_the_map_and_a_decision_in_a_conflict_says_so()
    {
        var map = Map();

        var anchor = Node(map, "feature:tabs.tab-manager");
        Assert.Equal((Labels.ContextMapInMap, Labels.ContextMapStartsHereFoot, true, false), (anchor.FootWord, anchor.FootRight, anchor.FootIsOk, anchor.FootIsWarn));
        var decision = map.Nodes.First(n => n.Layout.Kind == "decision");
        Assert.Equal((Labels.ContextMapConflictFoot, true, true), (decision.FootWord, decision.FootIsWarn, decision.HasFoot));
        Assert.False(Node(map, "feature:tabs.tab-groups").HasFoot);
    }

    [Fact]
    public void Chips_and_cards_are_told_apart_and_files_draw_in_the_mono_face()
    {
        var map = Map();

        Assert.True(Node(map, "feature:tabs.tab-manager").IsCard);
        Assert.True(Node(map, "feature:tabs.tab-groups").IsChip);
        Assert.True(Node(map, "code:src/tabs/TabManager.ts").IsMono);
        Assert.False(Node(map, "feature:tabs.tab-groups").IsMono);
        Assert.Equal("Kind.Feature", Node(map, "feature:tabs.tab-groups").KindIcon);
        Assert.Equal("Kind.Dot", Node(map, "surface:command/group-tabs").KindIcon);   // a kind this build does not know gets the plain dot
    }

    [Fact]
    public void A_word_this_build_does_not_know_reads_raw()
    {
        var map = Map();

        Assert.Equal("surface: Group tabs (command)", Node(map, "surface:command/group-tabs").Name);
    }

    [Fact]
    public void There_is_a_pill_for_every_edge_and_only_cross_band_and_conflict_pills_are_always_on()
    {
        var map = Map();

        Assert.Equal(map.Edges.Count, map.Pills.Count);
        Assert.All(map.Pills.Where(p => _quiet.Contains(p.Edge.Type)), p => Assert.False(p.Always));
        Assert.All(map.Pills.Where(p => _loud.Contains(p.Edge.Type)), p => Assert.True(p.Always));
    }

    [Fact]
    public void The_band_frames_carry_their_names_and_counts_and_the_world_their_size()
    {
        var map = Map();

        Assert.Equal([Labels.ContextMapBandIntent, Labels.ContextMapBandProduct, Labels.ContextMapBandCode], map.Bands.Select(b => b.Title), StringComparer.Ordinal);
        Assert.Equal(map.Bands[0].Title + ". " + map.Bands[0].Meta, map.Bands[0].Name);
        Assert.Equal((1360d, 872d), (map.WorldWidth, map.WorldHeight));
    }

    [Fact]
    public void The_control_row_counts_the_starting_points_and_marks_a_draft()
    {
        var plain = Map();
        var draft = Map(node => node["draft"] = true);

        Assert.Equal([("feature:tabs.tab-manager", "Tab manager", "feature")], plain.Anchors.Select(a => (a.Key, a.Label, a.Kind)));
        Assert.False(plain.Draft);
        Assert.True(draft.Draft);
    }

    [Fact]
    public void Coverage_is_four_areas_with_a_sentence_built_on_the_client()
    {
        var map = Map();

        Assert.Equal([Labels.ContextMapAreaIntent, Labels.ContextMapAreaProduct, Labels.ContextMapAreaCode, Labels.ContextMapAreaNames], map.CoverageAreas.Select(a => a.Name), StringComparer.Ordinal);
        Assert.Equal([CoverageLevel.Limited, CoverageLevel.Searched, CoverageLevel.Limited, CoverageLevel.NotAvailable], map.CoverageAreas.Select(a => a.Level));
        Assert.Equal(Labels.ContextMapCoverageLead(1, 4), map.CoverageLead);
        Assert.Equal(Labels.ContextMapCoverageRest([Labels.ContextMapAreaIntent, Labels.ContextMapAreaCode]), map.CoverageRest);
        Assert.Equal(Labels.ContextMapNamesDetail, map.CoverageAreas[3].Detail);
        Assert.Equal([Labels.ContextMapLimited, Labels.ContextMapSearched, Labels.ContextMapLimited, Labels.ContextMapNotAvailable], map.CoverageAreas.Select(a => a.LevelWord), StringComparer.Ordinal);
    }

    [Fact]
    public void An_empty_band_is_not_available_and_a_fully_covered_one_is_searched()
    {
        var map = Map(node =>
        {
            node["searched"]!["bands"]!["intent"]!["empty"] = true;
            node["coverage"]!["code"]!["implemented"] = 6;
            node["bands"]!["code"]!["symbols"]!["state"] = "ok";
        });

        Assert.Equal([CoverageLevel.NotAvailable, CoverageLevel.Searched, CoverageLevel.Searched, CoverageLevel.Searched], map.CoverageAreas.Select(a => a.Level));
        Assert.Equal(Labels.ContextMapCoverageLead(3, 4), map.CoverageLead);
        Assert.Equal(string.Empty, map.CoverageRest);
    }

    [Theory]
    [InlineData(1, "A is limited.")]
    [InlineData(2, "A and B are limited.")]
    [InlineData(3, "A, B and C are limited.")]
    public void The_sentence_names_the_limited_areas(int count, string words) => Assert.Equal(words, Labels.ContextMapCoverageRest(_letters[..count]));
}
