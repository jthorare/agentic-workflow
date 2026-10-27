using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;

namespace Aw.Context.Tests.ConceptScan;

public sealed class ConceptScanTiersTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(12, 800, ModelTier.Cheap)]
    [InlineData(150, 10_000, ModelTier.Cheap)]
    [InlineData(151, 800, ModelTier.Strong)]
    [InlineData(12, 10_001, ModelTier.Strong)]
    [InlineData(400, 14_000, ModelTier.Strong)]
    public void The_defaults_pick_strong_above_either_threshold_and_cheap_otherwise(int features, int promptTokens, ModelTier expected) =>
        Assert.Equal(expected, ConceptScanTiers.Default.Pick(features, promptTokens));

    [Fact, Trait("Lane", "unit")]
    public void Other_thresholds_move_the_line() =>
        Assert.Equal(ModelTier.Strong, new ConceptScanTiers(StrongAboveFeatures: 10, StrongAbovePromptTokens: 500).Pick(11, 1));

    [Theory, Trait("Lane", "unit")]
    [InlineData("", 0)]
    [InlineData("abcd", 1)]
    [InlineData("abcde", 2)]
    public void Tokens_are_estimated_as_characters_over_four_rounded_up(string prompt, int expected) =>
        Assert.Equal(expected, ConceptScanTiers.EstimateTokens(prompt));
}
