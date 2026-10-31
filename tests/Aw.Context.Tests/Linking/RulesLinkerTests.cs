using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Linking;

public sealed class RulesLinkerTests
{
    private static readonly RecordKey _r103 = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000103");
    private static readonly RecordKey _r104 = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000104");
    private static readonly RecordKey _other = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000999");
    private static readonly IReadOnlySet<string> _teams = new HashSet<string>(StringComparer.Ordinal) { "SPL" };

    private static readonly IReadOnlyList<IntentRecord> _known =
    [
        new(_r103, SourceId.From("linear"), "2026-07-23T09:00:00+00:00", "Toggle split orientation", ["SPL-103"]),
        new(_r104, SourceId.From("linear"), "2026-07-22T10:20:00+00:00", "Restore pane layout when switching tabs", ["SPL-104"]),
        new(_other, SourceId.From("linear"), "2026-07-22T10:20:00+00:00", "Another team's issue", ["OTH-7"]),
    ];

    private static GraphSnapshot Map() => new GraphBuilder()
        .Node("feature:layout.split-pane", "Split pane")
        .Node("feature:layout.split-pane.orientation", "Split orientation")
        .Node("feature:tabs.tab-manager", "Tabs")
        .Node("surface:command/toggle-orientation", "Toggle split orientation (command)")
        .Node("surface:setting/paneResizeStep", "paneResizeStep setting")
        .Node("code:src/layout/SplitPane.ts", "SplitPane.ts", Provenance.Deterministic)
        .Edge("code:src/layout/SplitPane.ts", EdgeType.Implements, "feature:layout.split-pane", provenance: Provenance.Deterministic)
        .Edge("code:src/layout/SplitPane.ts", EdgeType.Implements, "feature:layout.split-pane.orientation", provenance: Provenance.Model, confidence: 0.4)
        .Alias("feature:layout.orientation", "feature:layout.split-pane.orientation")
        .Build();

    private static string Sha(string span) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(span)));

    [Fact, Trait("Lane", "unit")]
    public void IssueKey_in_text_links_record_to_record_only_for_profile_teams()
    {
        var links = RulesLinker.Link(_r103, "Depends on SPL-104, not on OTH-7; this one is SPL-103.", Map(), (_known, _teams), []);

        var link = Assert.Single(links);
        Assert.Equal("record:" + _r104.Value, link.Target.Value);
        Assert.Equal(LinkRule.IssueKey, link.Rule);
        Assert.Equal(_r103, link.Record);
        Assert.Equal(1.0, link.Confidence);
    }

    [Fact, Trait("Lane", "unit")]
    public void IssueKey_of_a_record_not_synced_yet_links_by_key_so_the_result_does_not_depend_on_fetch_order()
    {
        var links = RulesLinker.Link(_r103, "Depends on SPL-105 and https://linear.app/fixture-labs/issue/SPL-106/later.", Map(), (_known, _teams), []);

        Assert.Equal(["record:SPL-105", "record:SPL-106"], links.Select(l => l.Target.Value), StringComparer.Ordinal);
        Assert.Equal([LinkRule.IssueKey, LinkRule.Url], links.Select(l => l.Rule));
    }

    [Fact, Trait("Lane", "unit")]
    public void Linear_issue_url_links_like_its_key()
    {
        var links = RulesLinker.Link(_r103, "See https://linear.app/fixture-labs/issue/SPL-104/restore-layout for the follow-up.", Map(), (_known, _teams), []);

        var link = Assert.Single(links);
        Assert.Equal("record:" + _r104.Value, link.Target.Value);
        Assert.Equal(LinkRule.Url, link.Rule);
    }

    [Fact, Trait("Lane", "unit")]
    public void Repo_path_in_text_links_code_node_and_the_nodes_it_implements_when_confirmed()
    {
        var links = RulesLinker.Link(_r103, "Implemented in src/layout/SplitPane.ts, nothing else.", Map(), (_known, _teams), []);

        var targets = links.Select(l => l.Target.Value).ToList();
        Assert.Equal(["code:src/layout/SplitPane.ts", "feature:layout.split-pane"], targets);
        Assert.All(links, l => Assert.Equal(LinkRule.Path, l.Rule));
        Assert.All(links, l => Assert.Equal(0.8, l.Confidence));
    }

    [Fact, Trait("Lane", "unit")]
    public void Identifier_token_matches_node_key_tail_alias_or_label_on_word_boundaries()
    {
        var links = RulesLinker.Link(_r103, "Add the toggle-orientation command, keep paneResizeStep stable and use layout.orientation.", Map(), (_known, _teams), []);

        var identified = links.Where(l => l.Rule == LinkRule.Identifier).ToList();   // "layout.orientation" also names a feature by alias, which the name rule suggests apart
        Assert.Equal(
            ["feature:layout.split-pane.orientation", "surface:command/toggle-orientation", "surface:setting/paneResizeStep"],
            identified.Select(l => l.Target.Value).ToList());

        var inside = RulesLinker.Link(_r103, "Fix pretoggle-orientations and xpaneResizeStepy later.", Map(), (_known, _teams), []);
        Assert.Empty(inside);
    }

    [Fact, Trait("Lane", "unit")]
    public void Quoted_title_links_the_node_with_that_label()
    {
        var links = RulesLinker.Link(_r103, "The \"Split orientation\" work is done; 'Tabs' is next.", Map(), (_known, _teams), []);

        Assert.Equal(["feature:layout.split-pane.orientation", "feature:tabs.tab-manager"], links.Select(l => l.Target.Value).ToList());
        Assert.All(links, l => Assert.Equal(LinkRule.QuotedTitle, l.Rule));
    }

    [Fact, Trait("Lane", "unit")]
    public void Plain_english_words_never_link()
    {
        var links = RulesLinker.Link(_r103, "We split the tab, then the pane resize and the orientation of the tabs.", Map(), (_known, _teams), []);

        Assert.Empty(links);
    }

    [Fact, Trait("Lane", "unit")]
    public void Link_evidence_carries_the_span_hash_not_the_text()
    {
        var links = RulesLinker.Link(_r103, "Implemented in src/layout/SplitPane.ts and SPL-104.", Map(), (_known, _teams), []);

        Assert.Equal(Sha("src/layout/SplitPane.ts"), links.First(l => l.Rule == LinkRule.Path).SpanHash);
        Assert.Equal(Sha("SPL-104"), links.First(l => l.Rule == LinkRule.IssueKey).SpanHash);
        Assert.DoesNotContain(links, l => l.SpanHash.Contains("SplitPane", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Same_record_version_links_identically()
    {
        const string text = "SPL-104 touches src/layout/SplitPane.ts and the \"Tabs\" feature via toggle-orientation.";

        var first = RulesLinker.Link(_r103, text, Map(), (_known, _teams), []);
        var second = RulesLinker.Link(_r103, text, Map(), (_known, _teams), []);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_features_label_as_whole_words_links_it_as_a_suggestion()
    {
        var links = RulesLinker.Link(_r103, "Users want the Split Pane to remember its size.", Map(), (_known, _teams), []);

        var link = Assert.Single(links);
        Assert.Equal(("feature:layout.split-pane", LinkRule.Name, 0.5), (link.Target.Value, link.Rule, link.Confidence));
        Assert.Equal(Sha("split pane"), link.SpanHash);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_alias_of_a_feature_with_two_words_links_it()
    {
        var links = RulesLinker.Link(_r103, "The layout orientation is wrong after a resize.", Map(), (_known, _teams), []);

        var link = Assert.Single(links);
        Assert.Equal(("feature:layout.split-pane.orientation", LinkRule.Name), (link.Target.Value, link.Rule));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_name_inside_a_longer_word_or_a_one_word_or_common_label_does_not_link()
    {
        var graph = new GraphBuilder().Node("feature:tabs", "Tabs").Node("feature:general", "General settings").Node("feature:split-pane", "Split pane").Build();

        Assert.Empty(RulesLinker.Link(_r103, "Tabs are fine; the general settings page; a resplit pane and splitpane.", graph, (_known, _teams), []));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_phrase_that_names_two_features_links_neither()
    {
        var graph = new GraphBuilder().Node("feature:a.split-pane", "Split pane").Node("feature:b.split-pane", "Split pane").Build();

        Assert.Empty(RulesLinker.Link(_r103, "The split pane is slow.", graph, (_known, _teams), []));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_longest_name_wins_where_names_overlap()
    {
        var links = RulesLinker.Link(_r103, "Fix the split orientation toggle.", Map(), (_known, _teams), []);

        Assert.Equal(["feature:layout.split-pane.orientation"], links.Select(l => l.Target.Value), StringComparer.Ordinal);
    }

    private static GraphSnapshot Concepts() => new GraphBuilder().Node("concept:invoicing", "Invoicing").Node("concept:credits", "Credits").Build();

    private static PinAliases Pin(string id, params string[] aliases) => new(ConceptId.From(id), aliases);

    [Fact, Trait("Lane", "unit")]
    public void A_one_word_alias_links_the_record_to_its_concept_at_point_eight_with_the_alias_as_written_as_evidence()
    {
        var links = RulesLinker.Aliases(_r103, "The invoice total is wrong.", Concepts(), [Pin("invoicing", "Invoice")]);

        var link = Assert.Single(links);
        Assert.Equal(("concept:invoicing", LinkRule.Alias, 0.8, _r103), (link.Target.Value, link.Rule, link.Confidence, link.Record));
        Assert.Equal(Sha("Invoice"), link.SpanHash);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_plural_matches_its_singular_both_ways_and_case_is_ignored()
    {
        _ = Assert.Single(RulesLinker.Aliases(_r103, "Invoices fail on export.", Concepts(), [Pin("invoicing", "invoice")]));
        _ = Assert.Single(RulesLinker.Aliases(_r103, "The INVOICE fails.", Concepts(), [Pin("invoicing", "invoices")]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_multi_word_alias_needs_its_words_next_to_each_other_in_order()
    {
        var pins = new[] { Pin("credits", "credit note") };

        Assert.Empty(RulesLinker.Aliases(_r103, "Add a note, then credit the customer; credit then note.", Concepts(), pins));
        Assert.Empty(RulesLinker.Aliases(_r103, "The note credit is wrong.", Concepts(), pins));
        Assert.Equal(["concept:credits"], RulesLinker.Aliases(_r103, "Print the credit notes in the report.", Concepts(), pins).Select(l => l.Target.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_whose_concept_node_is_not_in_the_map_offers_nothing()
    {
        var graph = new GraphBuilder().Node("concept:credits", "Credits").Build();

        Assert.Empty(RulesLinker.Aliases(_r103, "The invoice total is wrong.", graph, [Pin("invoicing", "invoice")]));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_alias_of_only_small_words_never_links() =>
        Assert.Empty(RulesLinker.Aliases(_r103, "What is the point of this, and the rest?", Concepts(), [Pin("invoicing", "the of", "  ", "of")]));

    [Fact, Trait("Lane", "unit")]
    public void Several_aliases_of_one_pin_make_one_link_and_the_links_are_ordered_by_target()
    {
        var links = RulesLinker.Aliases(_r103, "Invoices and bills and credit notes.", Concepts(), [Pin("invoicing", "bills", "invoice"), Pin("credits", "credit note")]);

        Assert.Equal(["concept:credits", "concept:invoicing"], links.Select(l => l.Target.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Link_includes_the_alias_links_beside_the_other_rules()
    {
        var graph = new GraphBuilder().Node("concept:invoicing", "Invoicing").Node("feature:tabs.tab-manager", "Tabs").Node("code:src/layout/SplitPane.ts", "SplitPane.ts", Provenance.Deterministic).Build();

        var links = RulesLinker.Link(_r103, "Invoices break in src/layout/SplitPane.ts.", graph, (_known, _teams), [Pin("invoicing", "invoice")]);

        Assert.Equal([("code:src/layout/SplitPane.ts", LinkRule.Path), ("concept:invoicing", LinkRule.Alias)], links.Select(l => (l.Target.Value, l.Rule)));
    }
}
