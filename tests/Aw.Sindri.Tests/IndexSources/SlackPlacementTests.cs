using Aw.Connectors.Core;
using Aw.Sindri.Host.IndexSources;

namespace Aw.Sindri.Tests.IndexSources;

public sealed class SlackPlacementTests
{
    private static readonly DateTimeOffset _t = new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    private static ConnectorItem Item(string key) => new(key, "message", "#general: hi", "hi", "Ada", "https://example.slack.com/archives/C0001GEN/p1", "open", "open", _t, _t, ClosedAt: null, "v1", [], Repo: null);

    [Theory, Trait("Lane", "unit")]
    [InlineData("slack:C0001GEN/1790000000.000100", "slack:C0001GEN")]
    [InlineData("slack:G0002OPS/day/2026-10-27", "slack:G0002OPS")]
    public void A_record_is_visible_to_its_channel_only_and_belongs_to_no_repo(string key, string audience)
    {
        var placement = new SlackPlacement(["C0001GEN", "G0002OPS"]).Place(Item(key));

        Assert.NotNull(placement);
        Assert.Equal(audience, placement.Audience.Value);
        Assert.Null(placement.Repo);
        Assert.Null(placement.SourceRepo);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("slack:C0009OTH/1790000000.000100")]
    [InlineData("slack:C0001GEN")]
    [InlineData("github:issue/acme/widgets/1")]
    [InlineData("slack:D0003DMX/1790000000.000100")]
    public void A_record_of_a_channel_the_profile_does_not_list_or_a_key_that_is_not_slacks_has_no_placement(string key)
        => Assert.Null(new SlackPlacement(["C0001GEN"]).Place(Item(key)));
}
