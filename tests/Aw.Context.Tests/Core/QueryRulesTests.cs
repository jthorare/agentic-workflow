using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Core;

public sealed class QueryRulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void Text_becomes_distinct_lower_case_terms_and_the_budget_is_capped()
    {
        var valid = QueryRules.Check(new ContextQuery("Split pane: ORIENTATION, split-pane!", Budget: 50_000), new ContextSettings { MaxBudget = 6_000 });

        Assert.Equal(["split", "pane", "orientation"], valid.Value.Terms);
        Assert.Equal(6_000, valid.Value.Budget);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_budget_gets_the_default() =>
        Assert.Equal(QueryRules.DefaultBudget, QueryRules.Check(new ContextQuery("x"), new ContextSettings()).Value.Budget);

    [Theory, Trait("Lane", "unit")]
    [InlineData("  ,; ")]
    [InlineData(null)]
    public void A_question_without_words_or_shape_is_006(string? text)
    {
        var checkedQuery = QueryRules.Check(new ContextQuery(text), new ContextSettings());

        Assert.Equal(ContextErrors.QueryInvalid.Code, checkedQuery.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_shapes_a_bad_depth_or_a_bad_budget_are_006()
    {
        Assert.Equal(ContextErrors.QueryInvalid.Code, QueryRules.Check(new ContextQuery("x", Node: "feature:a"), new()).Error.Code);
        Assert.Equal(ContextErrors.QueryInvalid.Code, QueryRules.Check(new ContextQuery("x", Depth: -1), new()).Error.Code);
        Assert.Equal(ContextErrors.QueryInvalid.Code, QueryRules.Check(new ContextQuery("x", Budget: 0), new()).Error.Code);
        Assert.True(QueryRules.Check(new ContextQuery(new string('a', 2_001)), new()).IsSuccess);   // a question has no length limit (owner q93)
        Assert.True(QueryRules.Check(new ContextQuery(string.Join(' ', Enumerable.Repeat("word", 5_000))), new()).IsSuccess);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, null)]
    [InlineData("0", 0)]
    [InlineData("4", 4)]
    [InlineData("8", 8)]
    [InlineData("9", 8)]
    [InlineData("500", 8)]
    public void A_depth_from_text_is_absent_or_a_whole_number_read_up_to_8(string? text, int? expected) =>
        Assert.Equal(expected, QueryRules.ParseDepth(text).Value);

    [Theory, Trait("Lane", "unit")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("")]
    [InlineData(" ")]
    public void A_depth_that_is_not_a_whole_number_is_006_without_a_transport_hint(string text)
    {
        var error = QueryRules.ParseDepth(text).Error;

        Assert.Equal(ContextErrors.QueryInvalid.Code, error.Code);
        Assert.Equal("depth must be a whole number of 0 or more (deeper than 8 is read as 8)", error.Message);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("-1")]
    [InlineData("99999999999999999999")]
    public void A_negative_or_oversized_whole_depth_is_out_of_range_not_a_non_number(string text)
    {
        var error = QueryRules.ParseDepth(text).Error;

        Assert.Equal(ContextErrors.QueryInvalid.Code, error.Code);
        Assert.Contains("out of range", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("is not a whole number", error.Message, StringComparison.Ordinal);
        Assert.StartsWith("depth must be a whole number of 0 or more (deeper than 8 is read as 8)", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, null)]
    [InlineData("1", 1)]
    [InlineData("8000", 8000)]
    public void A_budget_from_text_is_absent_or_a_positive_whole_number(string? text, int? expected) =>
        Assert.Equal(expected, QueryRules.ParseBudget(text).Value);

    [Theory, Trait("Lane", "unit")]
    [InlineData("abc", "budget must be a whole number of tokens, 1 or more")]
    [InlineData("0", "out of range")]
    [InlineData("-5", "out of range")]
    public void A_bad_budget_is_006(string text, string message)
    {
        var error = QueryRules.ParseBudget(text).Error;

        Assert.Equal(ContextErrors.QueryInvalid.Code, error.Code);
        Assert.Contains(message, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_depth_check_for_a_number_gives_the_same_006_as_the_text_parse()
    {
        Assert.Null(QueryRules.CheckDepth(depth: null));
        Assert.Null(QueryRules.CheckDepth(3));
        Assert.Null(QueryRules.CheckDepth(9));   // deeper than 8 is clamped where it is read, not refused
        Assert.Equal(QueryRules.ParseDepth("-1").Error, QueryRules.CheckDepth(-1));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_symbol_inside_a_file_is_unknown_until_the_code_index_phase() =>
        Assert.Equal(ContextErrors.UnknownAnchor.Code, QueryRules.Check(new ContextQuery(Text: null, Node: null, Item: null, Symbol: "src/a.ts#render"), new()).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void An_item_anchor_is_a_key_with_no_terms_of_its_own_and_a_blank_or_control_one_is_006()
    {
        Assert.Empty(QueryRules.Check(new ContextQuery(Text: null, Item: "ENG-1"), new()).Value.Terms);
        Assert.Equal(ContextErrors.QueryInvalid.Code, QueryRules.Check(new ContextQuery(Text: null, Item: "  "), new()).Error.Code);
        Assert.Equal(ContextErrors.QueryInvalid.Code, QueryRules.Check(new ContextQuery(Text: null, Item: "ENG-1\nx"), new()).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_symbol_in_its_file_path_form_passes_with_no_terms_of_its_own_and_an_empty_one_is_006()
    {
        var valid = QueryRules.Check(new ContextQuery(Text: null, Symbol: "src/a.ts"), new()).Value;

        Assert.Empty(valid.Terms);
        Assert.Equal(ContextErrors.QueryInvalid.Code, QueryRules.Check(new ContextQuery(Text: null, Symbol: "  "), new()).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_node_anchor_passes_with_no_terms_of_its_own_and_an_empty_one_is_006()
    {
        var node = QueryRules.Check(new ContextQuery(Text: null, Node: "feature:layout.split-pane"), new());
        var empty = QueryRules.Check(new ContextQuery(Text: null, Node: " "), new());

        Assert.Empty(node.Value.Terms);
        Assert.Equal(ContextErrors.QueryInvalid.Code, empty.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Terms_stop_at_sixty_four_and_words_at_64_characters()
    {
        var terms = QueryRules.Terms(string.Join(' ', Enumerable.Range(0, 80).Select(i => "w" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))) + " " + new string('z', 70));

        Assert.Equal(64, QueryRules.MaxTerms);
        Assert.Equal(QueryRules.MaxTerms, terms.Count);
        Assert.Equal(64, QueryRules.Terms(new string('z', 70)).Single().Length);
    }

    [Fact, Trait("Lane", "unit")]
    public void Words_take_the_cap_of_the_caller_and_Terms_keeps_sixty_four()
    {
        var text = string.Join(' ', Enumerable.Range(0, 40).Select(i => "w" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Equal(40, QueryRules.Words(text, 256).Count);
        Assert.Equal(5, QueryRules.Words(text, 5).Count);
        Assert.Equal(QueryRules.Words(text, QueryRules.MaxTerms), QueryRules.Terms(text));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_shape_and_node_006_messages_name_no_cli_flag()
    {
        var shapes = QueryRules.Check(new ContextQuery("x", Node: "feature:a"), new()).Error.Message;
        var node = QueryRules.Check(new ContextQuery(Text: null, Node: new string('n', 2_001)), new()).Error.Message;

        Assert.DoesNotContain("--", shapes, StringComparison.Ordinal);
        Assert.DoesNotContain("--", node, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("split pane", true)]
    [InlineData("", true)]
    public void An_ascii_value_fits_the_query_url(string value, bool fits) =>
        Assert.Equal(fits, QueryRules.FitsQueryUrl(value));

    [Fact, Trait("Lane", "unit")]
    public void A_long_non_ascii_value_does_not_fit_the_query_url_though_it_fits_the_text_cap()
    {
        var text = new string('\u6f22', 900);

        Assert.True(text.Length <= QueryRules.MaxTextLength);
        Assert.False(QueryRules.FitsQueryUrl(text));
        Assert.True(Uri.EscapeDataString(new string('a', QueryRules.MaxEncodedText)).Length == QueryRules.MaxEncodedText);
        Assert.True(QueryRules.FitsQueryUrl(new string('a', QueryRules.MaxEncodedText)));
        Assert.False(QueryRules.FitsQueryUrl(new string('a', QueryRules.MaxEncodedText + 1)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_subgraph_url_takes_20_distinct_keys_that_each_fit_and_nothing_more()
    {
        var twenty = Enumerable.Range(0, 20).Select(i => "feature:k." + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();

        Assert.Null(QueryRules.CheckSubgraphGet(twenty));
        Assert.Null(QueryRules.CheckSubgraphGet([.. twenty, .. twenty]));
        Assert.Equal(ContextErrors.QueryInvalid.Code, QueryRules.CheckSubgraphGet([.. twenty, "feature:k.20"])!.Code);
        Assert.Equal(ContextErrors.QueryInvalid.Code, QueryRules.CheckSubgraphGet(["feature:" + new string('\u6f22', 900)])!.Code);
    }
    [Theory, Trait("Lane", "unit")]
    [InlineData(null, null)]
    [InlineData("component", CascadeHops.Component)]
    [InlineData("state,flow,dependency,component", CascadeHops.Default | CascadeHops.Component)]
    [InlineData(" Flow , state ,flow", CascadeHops.State | CascadeHops.Flow)]
    public void Hops_from_text_are_absent_or_a_comma_list_in_any_order_case_and_repeat(string? text, CascadeHops? expected) =>
        Assert.Equal(expected, QueryRules.ParseHops(text).Value);

    [Theory, Trait("Lane", "unit")]
    [InlineData("bogus")]
    [InlineData("state,neighbors")]
    [InlineData("")]
    [InlineData("state,,flow")]
    [InlineData("8")]
    [InlineData("none")]
    [InlineData("Default")]
    public void An_unknown_or_empty_hop_is_140(string text) =>
        Assert.Equal(ContextErrors.HopsInvalid.Code, QueryRules.ParseHops(text).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void A_profile_fan_out_of_5_reaches_the_walk_through_CascadeFor_so_a_unit_of_6_features_is_skipped()
    {
        var edges = Enumerable.Range(0, 6).Select(i => new GraphEdge(NodeKey.From("code:src/util.ts"), NodeKey.From(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"feature:f{i}")), EdgeType.Implements)).ToList();
        var graph = new GraphSnapshot([.. edges.SelectMany(e => new[] { e.From, e.To }).DistinctBy(k => k.Value, StringComparer.Ordinal).Select(k => new GraphNode(k, k.Value))], edges, [], []);
        var hops = QueryRules.ParseHops("component").Value;

        var lowered = Cascade.Affects(graph, NodeKey.From("feature:f0"), depth: 1, QueryRules.CascadeFor(hops, new ContextSettings { CascadeComponentFanOut = 5 }));
        var byDefault = Cascade.Affects(graph, NodeKey.From("feature:f0"), depth: 1, QueryRules.CascadeFor(hops, new ContextSettings()));

        _ = Assert.Single(lowered.SkippedComponents);
        Assert.Empty(lowered.Affected);
        Assert.Empty(byDefault.SkippedComponents);   // the default fan-out is 50 now (owner q93, 2026-10-23)
        Assert.Equal(5, byDefault.Affected.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_query_with_a_bad_hops_list_is_140_before_any_source_sees_it() =>
        Assert.Equal(ContextErrors.HopsInvalid.Code, QueryRules.Check(new ContextQuery("x") { Hops = "bogus" }, new()).Error.Code);
}
