using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Graph;

/// <summary>A choice label through the answer's scrubber (MapText.ChoiceLabel, #4196 review of #4197): a long one is cut with "…", and one with a control character shows its key, scrubbed.</summary>
public sealed class AnchorChoiceLabelTests
{
    private static Func<string, string> Scrubbed() => new MapText(new FakeScrubberFactory().For([]).Value).ChoiceLabel;

    [Theory, Trait("Lane", "unit")]
    [InlineData(201)]
    [InlineData(500)]
    public void A_long_label_through_the_scrubber_is_cut_to_the_cap_and_ends_in_an_ellipsis(int length)
    {
        var choice = Assert.Single(AnchorMatch.Choices(new GraphBuilder().Node("feature:a", new string('a', length)).Build().Nodes, Scrubbed()));

        Assert.Equal(new string('a', 199) + "…", choice.Label);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_label_with_a_control_character_shows_the_key_through_the_scrubber_and_the_pick_reads_the_raw_key()
    {
        var key = "concept:tabs." + FakeScrubber.Secret;

        var choice = Assert.Single(AnchorMatch.Choices(new GraphBuilder().Node(key, "Tab\u0007groups").Build().Nodes, Scrubbed()));

        Assert.Equal((key, "concept:tabs.[REDACTED:fake]"), (choice.Key, choice.Label));
    }
}
