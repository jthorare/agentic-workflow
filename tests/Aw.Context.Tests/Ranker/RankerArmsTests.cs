using Aw.Context.Core;
using Aw.Context.Core.Eval.Backtest;

namespace Aw.Context.Tests.Ranker;

public sealed class RankerArmsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_ranker_arm_expands_to_the_three_rows_in_order()
    {
        var parsed = Arms.Parse("ranker");

        Assert.Equal([Arm.RankerRrf, Arm.RankerFeatures, Arm.RankerRerank], parsed.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_arms_have_their_names_and_run_today()
    {
        Assert.Equal(["ranker-rrf", "ranker-features", "ranker-rerank"], Arms.Ranker.Select(Arms.Name), StringComparer.Ordinal);
        Assert.All(Arms.Ranker, a => Assert.Null(Arms.PendingStage(a)));
    }

    [Fact, Trait("Lane", "unit")]
    public void All_does_not_run_the_rerank_row_because_it_asks_a_model() =>
        Assert.DoesNotContain(Arm.RankerRerank, Arms.Parse("all").Value);

    [Fact, Trait("Lane", "unit")]
    public void Ranker_arms_mix_with_the_others_by_name_in_report_order_without_repeats() =>
        Assert.Equal([Arm.Graph, Arm.RankerRrf, Arm.RankerRerank], Arms.Parse("ranker-rerank, graph,ranker-rrf,graph").Value);

    [Fact, Trait("Lane", "unit")]
    public void The_ranker_token_and_one_of_its_rows_do_not_repeat() =>
        Assert.Equal([Arm.RankerRrf, Arm.RankerFeatures, Arm.RankerRerank], Arms.Parse("ranker,ranker-rrf").Value);

    [Fact, Trait("Lane", "unit")]
    public void A_misspelt_ranker_arm_is_SND_CONTEXT_082_listing_the_ranker_arms()
    {
        var parsed = Arms.Parse("rankers");

        Assert.Equal(ContextErrors.UnknownArm.Code, parsed.Error.Code);
        Assert.Contains("ranker-features", parsed.Error.Message, StringComparison.Ordinal);
    }
}
