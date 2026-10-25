using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Graph;

public sealed class AnchorMatchTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("feature:layout.split-pane.orientation", "feature:layout.split-pane.orientation")]
    [InlineData("feature:layout.orientation", "feature:layout.split-pane.orientation")]
    [InlineData("split ORIENTATION", "feature:layout.split-pane.orientation")]
    [InlineData("Tabs", "feature:tabs.tab-manager")]
    public void A_key_an_alias_or_a_label_resolves_to_its_node(string name, string expected)
    {
        var node = AnchorMatch.Resolve(FixtureGraph.Applied(), name);

        Assert.Equal(expected, node.Value.Key.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_name_whose_words_all_sit_in_one_feature_label_resolves_to_that_feature_in_any_order()
    {
        var graph = new GraphBuilder().Node("feature:staff.self-scheduling", "Staff self scheduling").Node("feature:staff.pay", "Staff pay").Build();

        var node = AnchorMatch.Resolve(graph, "scheduling, staff");

        Assert.Equal("feature:staff.self-scheduling", node.Value.Key.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_name_whose_words_sit_in_two_feature_labels_stays_unknown()
    {
        var graph = new GraphBuilder().Node("feature:a", "Staff self scheduling").Node("feature:b", "Staff open scheduling").Build();

        var node = AnchorMatch.Resolve(graph, "staff scheduling");

        Assert.Equal(ContextErrors.UnknownAnchor.Code, node.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Suggestions_list_a_test_file_after_other_nodes_with_the_same_word_count()
    {
        var graph = new GraphBuilder().Node("code:src/a/__tests__/widget.test.ts", "widget.test.ts").Node("feature:widget", "Widget").Build();

        var node = AnchorMatch.Resolve(graph, "widget gadget");

        Assert.Equal(ContextErrors.UnknownAnchor.Code, node.Error.Code);
        Assert.True(node.Error.Message.IndexOf("feature:widget", StringComparison.Ordinal) < node.Error.Message.IndexOf("code:src/a", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_label_two_nodes_share_is_SND_CONTEXT_037_listing_both()
    {
        var graph = new GraphBuilder().Node("feature:a", "Same").Node("feature:b", "same").Build();

        var node = AnchorMatch.Resolve(graph, "Same");

        Assert.Equal(ContextErrors.AmbiguousAnchor.Code, node.Error.Code);
        Assert.Contains("feature:a", node.Error.Message, StringComparison.Ordinal);
        Assert.Contains("feature:b", node.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_name_is_SND_CONTEXT_003_with_suggestions()
    {
        var node = AnchorMatch.Resolve(FixtureGraph.Applied(), "orientation toggles");

        Assert.Equal(ContextErrors.UnknownAnchor.Code, node.Error.Code);
        Assert.Contains("feature:layout.split-pane.orientation", node.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void SND_CONTEXT_037_carries_the_candidate_keys_as_a_typed_list_and_keeps_its_detail()
    {
        var graph = new GraphBuilder().Node("feature:a", "Same").Node("feature:b", "same").Build();

        var node = AnchorMatch.Resolve(graph, "Same");

        Assert.Equal(["feature:a", "feature:b"], Members(node.Error, "candidates"));
        Assert.Equal("'Same' is the name of 2 nodes: feature:a, feature:b (fix: ask by key)", node.Error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_key_longer_than_a_problem_string_stays_in_the_detail_but_not_in_the_typed_lists()
    {
        var longKey = "feature:" + new string('x', 250);
        var graph = new GraphBuilder().Node("feature:a", "Same pane").Node(longKey, "same pane").Build();

        var ambiguous = AnchorMatch.Resolve(graph, "Same pane");
        var unknown = AnchorMatch.Resolve(graph, "pane thing");

        Assert.Equal(["feature:a"], Members(ambiguous.Error, "candidates"));
        Assert.Contains(longKey, ambiguous.Error.Message, StringComparison.Ordinal);
        Assert.Equal(["feature:a"], Members(unknown.Error, "suggestedAnchors"));
        Assert.Contains(longKey, unknown.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void SND_CONTEXT_037_candidates_list_every_key_and_so_does_the_detail()
    {
        var builder = new GraphBuilder();
        foreach (var i in Enumerable.Range(0, 120))
        {
            builder = builder.Node("feature:n" + i.ToString("000", System.Globalization.CultureInfo.InvariantCulture), "Same");
        }

        var node = AnchorMatch.Resolve(builder.Build(), "Same");

        Assert.Equal(120, Members(node.Error, "candidates").Count);   // why: no cap, so a list past 100 is whole
        Assert.Contains("feature:n119", node.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void SND_CONTEXT_003_carries_the_suggested_anchors_as_a_typed_list_matching_its_detail()
    {
        var node = AnchorMatch.Resolve(FixtureGraph.Applied(), "orientation toggles");

        var suggested = Members(node.Error, "suggestedAnchors");
        Assert.Contains(suggested, k => string.Equals(k, "feature:layout.split-pane.orientation", StringComparison.Ordinal));
        Assert.InRange(suggested.Count, 1, 5);
        Assert.Contains("try " + string.Join(", ", suggested), node.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void SND_CONTEXT_003_with_nothing_to_suggest_has_no_suggestedAnchors_member()
    {
        var graph = new GraphBuilder().Node("feature:a", "Alpha").Build();

        var node = AnchorMatch.Resolve(graph, "zzz");

        Assert.Equal(ContextErrors.UnknownAnchor.Code, node.Error.Code);
        Assert.DoesNotContain(node.Error.Extensions ?? Kernel.ErrorExtensions.Empty, m => m.Key == "suggestedAnchors");
    }

    private static List<string> Members(Kernel.Error error, string name)
    {
        var member = Assert.Single(error.Extensions ?? Kernel.ErrorExtensions.Empty, m => m.Key == name);
        return [.. member.Value.EnumerateArray().Select(e => e.GetString()!)];
    }

    [Fact, Trait("Lane", "unit")]
    public void A_node_named_after_more_than_sixteen_other_words_still_anchors()
    {
        var question = "When I change the split pane layout in the settings dialog of the editor window, what else in the app could break, for example Tabs?";

        var anchors = AnchorMatch.FromText(FixtureGraph.Applied(), question);

        Assert.Contains(anchors.Anchors, k => k.Value == "feature:tabs.tab-manager");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_question_naming_a_whole_label_anchors_on_it()
    {
        var anchors = AnchorMatch.FromText(FixtureGraph.Applied(), "What else is affected if I change how split orientation works?");

        Assert.Equal(["feature:layout.split-pane.orientation"], anchors.Anchors.Select(k => k.Value), StringComparer.Ordinal);
        Assert.Contains(anchors.Suggested, k => k.Value == "surface:command/toggle-orientation");
        Assert.True(anchors.Suggested.Count <= 5);
    }

    [Fact, Trait("Lane", "unit")]
    public void Plural_words_and_parenthesized_kinds_still_match_and_the_most_specific_label_wins()
    {
        var anchors = AnchorMatch.FromText(FixtureGraph.Applied(), "Which key splits a pane vertically?");

        Assert.Equal(["action:split-pane.split-vertical"], anchors.Anchors.Select(k => k.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_question_naming_nothing_has_no_anchors_and_no_suggestions()
    {
        var anchors = AnchorMatch.FromText(FixtureGraph.Applied(), "xyzzy plugh");

        Assert.Empty(anchors.Anchors);
        Assert.Empty(anchors.Suggested);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_label_with_this_or_does_anchors_on_a_question_that_says_the()
    {
        var graph = new GraphBuilder().Node("feature:close-tab", "Close this tab").Node("feature:other", "Other thing").Build();

        var anchors = AnchorMatch.FromText(graph, "what happens if I close the tab");

        Assert.Equal(["feature:close-tab"], anchors.Anchors.Select(k => k.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Small_words_that_lose_their_s_never_become_words_or_suggestions()
    {
        var graph = new GraphBuilder().Node("feature:a", "This does it").Node("feature:b", "Widgets").Build();

        var anchors = AnchorMatch.FromText(graph, "this does that");

        Assert.Empty(anchors.Suggested);
        Assert.Empty(anchors.Anchors);
        Assert.DoesNotContain("thi", AnchorMatch.Words("this does"));
        Assert.DoesNotContain("doe", AnchorMatch.Words("this does"));
        Assert.Empty(AnchorMatch.Words("this does"));
    }
}
