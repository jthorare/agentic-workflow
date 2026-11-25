using Aw.Meta.Core.Messages;

namespace Aw.Meta.Tests.Messages;

public sealed class MessagePreviewTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_body_of_80_characters_comes_back_whole()
    {
        var body = new string('a', 80);
        Assert.Equal(body, MessagePreview.Of(body));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_body_of_81_characters_is_cut_to_80_and_marked()
    {
        var body = new string('a', 81);
        Assert.Equal(new string('a', 80) + "...", MessagePreview.Of(body));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_body_stays_empty() => Assert.Equal(string.Empty, MessagePreview.Of(string.Empty));
}
