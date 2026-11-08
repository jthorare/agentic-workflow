using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Testing.Memory;

namespace Aw.Sindri.Tests.Memory;

/// <summary>The three match levels and the name rule.</summary>
[Lane(TestLane.Unit)]
public sealed class MatchLevelTests
{
    [Fact]
    public void A_stricter_level_has_a_higher_floor()
        => Assert.True(MatchLevels.Threshold(MatchLevel.Loose) < MatchLevels.Threshold(MatchLevel.Balanced) && MatchLevels.Threshold(MatchLevel.Balanced) < MatchLevels.Threshold(MatchLevel.Strict));

    [Fact]
    public void A_level_is_read_from_its_wire_name_and_defaults_to_balanced()
    {
        Assert.Equal(MatchLevel.Strict, MatchLevels.Parse("strict").Value);
        Assert.Equal(MatchLevel.Balanced, MatchLevels.Parse(null).Value);
        Assert.Equal(SindriErrors.MemoryMatchLevelInvalid.Code, MatchLevels.Parse("tight").Error.Code);
    }

    [Theory]
    [InlineData("team-notes", true)]
    [InlineData("a", true)]
    [InlineData("Team", false)]
    [InlineData("-lead", false)]
    [InlineData("has space", false)]
    [InlineData("", false)]
    public void A_memory_name_is_lowercase_letters_digits_and_hyphens(string text, bool valid)
    {
        var parsed = MemoryName.Parse(text);

        Assert.Equal(valid, parsed.IsSuccess);
        if (!valid)
        {
            Assert.Equal(SindriErrors.MemoryNameInvalid.Code, parsed.Error.Code);
        }
    }

    [Fact]
    public void Keywords_rank_by_the_share_of_query_tokens_found()
    {
        var both = FactOf("both", "deploy runs from main");
        var one = FactOf("one", "deploy happens on friday");
        var none = FactOf("none", "lunch at noon");

        var recall = KeywordRank.Rank("deploy main", [none, one, both], limit: 10);

        Assert.Equal(RecallMode.Keyword, recall.Mode);
        Assert.Equal(["both", "one"], recall.Facts.Select(static f => f.Fact.Key));
    }

    private static MemoryFact FactOf(string key, string text) => MemorySamples.Fact(key, text);
}
