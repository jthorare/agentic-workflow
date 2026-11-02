using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Wiring.WiringRecipe;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>A judge Step's 'parallel' takes a number or { profile: workflow.judge.parallel }, and no other profile key or a fact (RCP-BLOCK-003; issue 1440).</summary>
[Lane(TestLane.Unit)]
public sealed class ParallelProfileRulesTests
{
    [Fact]
    public void The_judge_parallel_profile_key_is_accepted() =>
        Assert.Empty(Check(Judge(new WithValue(Literal: null, Fact: null, "workflow.judge.parallel"))));

    [Fact]
    public void A_number_is_accepted() =>
        Assert.Empty(Check(Judge(Literal("4"))));

    [Fact]
    public void The_providers_key_is_refused_for_parallel()
    {
        var line = Assert.Single(Check(Judge(new WithValue(Literal: null, Fact: null, "workflow.judge.providers"))).Lines());

        Assert.StartsWith("RCP-BLOCK-003: parameter 'parallel' of 'step.scope.relevance-judge' takes a number or { profile: workflow.judge.parallel }, not workflow.judge.providers at ", line, StringComparison.Ordinal);
    }

    [Fact]
    public void A_fact_is_refused_for_parallel() =>
        Assert.StartsWith(
            "RCP-BLOCK-003: parameter 'parallel' of 'step.scope.relevance-judge' takes a number or { profile: workflow.judge.parallel }, not a fact at ",
            Assert.Single(Check(Judge(FactValue("fact:output"))).Lines()),
            StringComparison.Ordinal);

    private static IReadOnlyList<Core.Workflow.Validation.RecipeFinding> Check(Recipe recipe) =>
        CheckBlocks(recipe, new Located("r.yaml", [], Engine.EngineFixture.Catalog));

    private static Recipe Judge(WithValue parallel) =>
        Of("judge", [("judge", Active("step.scope.relevance-judge", with: With(("parallel", parallel)))), ("done", Terminal())],
            [Go("judge", "done", when: Outcome("ruled"))], inputs: ["fact:output"]);
}
