using Aw.Context.Core.Concepts;
using Aw.Context.Core.Map;

namespace Aw.Context.Tests.Concepts;

[Trait("Lane", "unit")]
public sealed class ConceptGlossaryReadTests
{
    private static ConceptGraph Terms() => new ConceptGraph()
        .ConceptWithTerms("concept:scheduling", "Self scheduling", "shift", "swap", "roster")
        .ConceptWithTerms("concept:billing", "Billing", "invoice", "roster")
        .Concept("concept:plain", "Plain")
        .Node("feature:shifts", "Shifts").Member("feature:shifts", "concept:scheduling");

    [Fact]
    public void A_concept_row_carries_its_terms_and_a_row_without_any_has_none()
    {
        var view = ConceptBuilder.Build(ConceptGraph.Basis, new ConceptsQuery(MapRead.Current, new ConceptPage()), Terms().Build(), ConceptGraph.Text).Value;

        Assert.Equal(["shift", "swap", "roster"], view.Concepts.Single(c => c.Id.Value == "concept:scheduling").Terms, StringComparer.Ordinal);
        Assert.Null(view.Concepts.Single(c => c.Id.Value == "concept:plain").Terms);
    }

    [Fact]
    public void The_glossary_lists_each_concept_with_its_terms_and_each_term_with_its_concepts()
    {
        var view = ConceptGlossaryBuilder.Build(ConceptGraph.Basis, Terms().Build(), ConceptGraph.Text);

        Assert.Equal((1, "v7", ConceptStates.Ok), (view.ApiVersion, view.MapVersion, view.State));
        Assert.Equal(["concept:billing", "concept:scheduling"], view.Concepts.Select(c => c.Id.Value), StringComparer.Ordinal);
        Assert.Equal(["shift", "swap", "roster"], view.Concepts.Single(c => c.Id.Value == "concept:scheduling").Terms, StringComparer.Ordinal);
        Assert.Equal(["invoice", "roster", "shift", "swap"], view.Terms.Select(t => t.Term), StringComparer.Ordinal);
        Assert.Equal(["concept:billing", "concept:scheduling"], view.Terms.Single(t => t.Term == "roster").Concepts.Select(c => c.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void A_map_whose_concepts_carry_no_term_has_an_empty_glossary()
    {
        var view = ConceptGlossaryBuilder.Build(ConceptGraph.Basis, ConceptGraph.Product().Build(), ConceptGraph.Text);

        Assert.Equal(ConceptStates.Empty, view.State);
        Assert.Empty(view.Concepts);
        Assert.Empty(view.Terms);
    }

    [Fact]
    public void The_stored_terms_make_a_glossary_of_the_member_features()
    {
        var graph = Terms().Member("feature:lone", "concept:scheduling").Build();

        var glossary = ConceptGlossaryBuilder.Glossary(graph);

        Assert.NotNull(glossary);
        Assert.Equal(["roster", "shift", "swap"], glossary.Terms.Select(t => t.Term).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        var roster = glossary.Terms.Single(t => t.Term == "roster");
        Assert.Equal(["feature:lone", "feature:shifts"], roster.Features.Select(f => f.Value), StringComparer.Ordinal);   // billing has no feature member
    }

    [Fact]
    public void A_term_the_concept_lists_first_weighs_more_than_one_it_lists_last()
    {
        var glossary = ConceptGlossaryBuilder.Glossary(Terms().Build());

        Assert.NotNull(glossary);
        Assert.True(glossary.Terms.Single(t => t.Term == "shift").Weight > glossary.Terms.Single(t => t.Term == "swap").Weight);
    }

    [Fact]
    public void A_map_whose_concepts_carry_no_term_has_no_stored_glossary() =>
        Assert.Null(ConceptGlossaryBuilder.Glossary(ConceptGraph.Product().Build()));
}
