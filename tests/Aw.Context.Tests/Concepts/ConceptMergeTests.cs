using Aw.Context.Core.Concepts;
using Aw.Context.Core.Map;

namespace Aw.Context.Tests.Concepts;

/// <summary>Issue 2975: near-duplicate suggested concepts fold into one row that names the others; a shared common word or a pin never folds.</summary>
[Trait("Lane", "unit")]
public sealed class ConceptMergeTests
{
    // Each case: the concepts in answer order, and what stays as 'id' or 'id: also1, also2'.
    private static readonly Dictionary<string, (ConceptTraits[] Concepts, string[] Folded)> _cases = new(StringComparer.Ordinal)
    {
        ["the three tab suggestions of the issue are one"] = (
            [
                Traits("concept:open-tabs", 2, "tab", "groups", "group", "strip", "manage"),
                Traits("concept:tab-grouping", 1, "tab", "groups", "group", "collapse", "expand"),
                Traits("concept:tab-pinning", 4, "tab", "groups", "group", "pin", "restore"),
            ],
            ["concept:tab-pinning: concept:open-tabs, concept:tab-grouping"]),
        ["one common term keeps two concepts apart"] = (
            [Traits("concept:scheduling", 1, "tab", "shift", "roster"), Traits("concept:billing", 1, "tab", "invoice", "refund")],
            ["concept:scheduling", "concept:billing"]),
        ["two shared terms out of five are not most"] = (
            [Traits("concept:a", 1, "tab", "group", "x1", "x2", "x3"), Traits("concept:b", 1, "tab", "group", "y1", "y2", "y3")],
            ["concept:a", "concept:b"]),
        ["most of the code files make one, terms or not"] = (
            [Files("concept:a", "src/A.cs", "src/B.cs", "src/C.cs"), Files("concept:b", "src/A.cs", "src/B.cs")],
            ["concept:a: concept:b"]),
        ["one shared file of one is too few"] = (
            [Files("concept:a", "src/A.cs"), Files("concept:b", "src/A.cs")],
            ["concept:a", "concept:b"]),
        ["terms match in any case"] = (
            [Traits("concept:a", 1, "Tab", "Group"), Traits("concept:b", 1, "tab", "group")],
            ["concept:a: concept:b"]),
        ["a pin never folds, nor takes a suggestion in"] = (
            [Pin("concept:pin", 9, "tab", "group", "pin"), Traits("concept:a", 1, "tab", "group", "pin"), Traits("concept:b", 1, "tab", "group", "pin")],
            ["concept:pin", "concept:a: concept:b"]),
        ["on a tie of members the earlier concept leads"] = (
            [Traits("concept:a", 1, "tab", "group"), Traits("concept:b", 1, "tab", "group")],
            ["concept:a: concept:b"]),
        ["a suggestion joins the lead only when it overlaps the lead itself"] = (
            [Traits("concept:lead", 3, "a", "b", "c"), Traits("concept:near", 2, "a", "b", "d", "e"), Traits("concept:far", 1, "d", "e", "f")],
            ["concept:lead: concept:near", "concept:far"]),
    };

    [Theory]
    [InlineData("the three tab suggestions of the issue are one")]
    [InlineData("one common term keeps two concepts apart")]
    [InlineData("two shared terms out of five are not most")]
    [InlineData("most of the code files make one, terms or not")]
    [InlineData("one shared file of one is too few")]
    [InlineData("terms match in any case")]
    [InlineData("a pin never folds, nor takes a suggestion in")]
    [InlineData("on a tie of members the earlier concept leads")]
    [InlineData("a suggestion joins the lead only when it overlaps the lead itself")]
    public void Suggestions_fold_when_they_share_most_terms_or_code_files(string name)
    {
        var (concepts, folded) = _cases[name];

        var actual = ConceptMerge.Fold(concepts).Select(f => f.Also.Count == 0 ? f.Id : f.Id + ": " + string.Join(", ", f.Also));

        Assert.Equal(folded, actual, StringComparer.Ordinal);
    }

    [Fact]
    public void The_answer_keeps_the_lead_row_names_the_others_and_counts_the_page_without_them()
    {
        var graph = new ConceptGraph()
            .ConceptWithTerms("concept:open-tabs", "Open tab management", "tab", "groups", "group", "strip", "manage")
            .ConceptWithTerms("concept:tab-grouping", "Tab grouping", "tab", "groups", "group", "collapse", "expand")
            .ConceptWithTerms("concept:tab-pinning", "Tab grouping and pinning", "tab", "groups", "group", "pin", "restore")
            .ConceptWithTerms("concept:billing", "Billing", "tab", "invoice", "refund")
            .Node("feature:strip", "Strip").Node("feature:pin", "Pin").Node("feature:restore", "Restore").Node("feature:invoices", "Invoices")
            .Member("feature:strip", "concept:open-tabs").Member("feature:pin", "concept:tab-pinning").Member("feature:restore", "concept:tab-pinning").Member("feature:invoices", "concept:billing")
            .Build();

        var view = ConceptBuilder.Build(ConceptGraph.Basis, new ConceptsQuery(MapRead.Current, new ConceptPage()), graph, ConceptGraph.Text).Value;

        Assert.Equal(["concept:billing", "concept:tab-pinning"], view.Concepts.Select(c => c.Id.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["Open tab management", "Tab grouping"], view.Concepts.Single(c => c.Id.Value == "concept:tab-pinning").Also, StringComparer.Ordinal);
        Assert.Null(view.Concepts.Single(c => c.Id.Value == "concept:billing").Also);
        Assert.Null(view.Next);
    }

    private static ConceptTraits Traits(string id, int members, params string[] terms) => new(id, terms, [], members, Pinned: false);

    private static ConceptTraits Pin(string id, int members, params string[] terms) => new(id, terms, [], members, Pinned: true);

    private static ConceptTraits Files(string id, params string[] code) => new(id, [], code, Members: 1, Pinned: false);
}
