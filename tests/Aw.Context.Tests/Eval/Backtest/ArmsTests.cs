using Aw.Context.Core;
using Aw.Context.Core.Eval.Backtest;

namespace Aw.Context.Tests.Eval.Backtest;

public sealed class ArmsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Arm_names_parse_all_expands_to_the_available_arms()
    {
        Assert.Equal([Arm.Brief, Arm.Docs, Arm.Graph, Arm.GraphReviewed, Arm.All], Arms.Parse("all").Value);
        Assert.Equal([Arm.Brief, Arm.Graph], Arms.Parse("graph, brief").Value);   // report order, not typed order
        Assert.Equal([Arm.Graph, Arm.Linear], Arms.Parse("linear,graph,graph").Value);   // a not-yet-available arm parses; the report prints it as not available
        Assert.Equal("graph+reviewed", Arms.Name(Arm.GraphReviewed));
        Assert.Equal(("S-6", "S-7B", "S-9", null), (Arms.PendingStage(Arm.Linear), Arms.PendingStage(Arm.Onyx), Arms.PendingStage(Arm.Code), Arms.PendingStage(Arm.Graph)));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("bogus")]
    [InlineData("")]
    [InlineData("graph,bogus")]
    public void Unknown_is_082_and_lists_the_arms(string text)
    {
        var parsed = Arms.Parse(text);

        Assert.Equal(ContextErrors.UnknownArm.Code, parsed.Error.Code);
        Assert.Contains("graph+reviewed", parsed.Error.Message, StringComparison.Ordinal);
        Assert.Contains("linear (S-6)", parsed.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_ranker_name_expands_to_the_three_ranker_arms()
    {
        var parsed = Arms.Parse("ranker");

        Assert.Equal(Arms.Ranker, parsed.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_extractor_version_is_pinned_so_a_recognizer_change_bumps_it() => Assert.Equal(2, Context.Core.Extract.MapExtractor.Version);

    [Fact, Trait("Lane", "unit")]
    public void S7A_allocates_080_to_083()
    {
        Assert.Equal(["SND-CONTEXT-080", "SND-CONTEXT-081", "SND-CONTEXT-082", "SND-CONTEXT-083"],
            new[] { ContextErrors.NoEligibleSample, ContextErrors.NoBriefSource, ContextErrors.UnknownArm, ContextErrors.AdjudicationCapped }.Select(e => e.Code), StringComparer.Ordinal);
        Assert.All([ContextErrors.NoEligibleSample, ContextErrors.NoBriefSource, ContextErrors.UnknownArm], e => Assert.Contains(e.Code, ContextErrors.Attention));
        Assert.DoesNotContain(ContextErrors.AdjudicationCapped.Code, ContextErrors.Attention);
    }
}
