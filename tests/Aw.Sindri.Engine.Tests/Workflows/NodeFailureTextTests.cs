using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Engine.Tests.Workflows;

/// <summary>#3304: the loop item's failure text and its split into the named text and the bare "code: message", tested directly (a review nit from #3774).</summary>
[Lane(TestLane.Unit)]
public sealed class NodeFailureTextTests
{
    [Fact]
    public void A_stopped_item_splits_into_the_named_text_and_the_bare_error()
    {
        var text = NodeFailureText.Stopped("bad", 1, "SND-CODE-004: boom");

        var (named, bare) = NodeFailureText.Split(text);

        Assert.Equal("node 'bad' failed on item 1: SND-CODE-004: boom", named);
        Assert.Equal("SND-CODE-004: boom", bare);
    }

    [Fact]
    public void A_stranded_item_splits_into_the_named_text_and_the_bare_error()
    {
        var text = NodeFailureText.Stranded("bad", 12, "SND-CODE-004: boom");

        var (named, bare) = NodeFailureText.Split(text);

        Assert.Equal(text, named);
        Assert.Equal("SND-CODE-004: boom", bare);
    }

    [Theory]
    [InlineData("SND-CODE-004: boom")]
    [InlineData("")]
    [InlineData("node 'bad' failed: SND-CODE-004: boom")]
    [InlineData("node 'bad' failed on item : SND-CODE-004: boom")]
    [InlineData("node 'bad' failed on item x: SND-CODE-004: boom")]
    [InlineData("node 'bad' failed on item 1")]
    [InlineData("node 'bad' failed on item 1 because: boom")]
    [InlineData("the node 'bad' failed on item 1: boom")]
    public void Text_that_does_not_match_the_pattern_has_no_named_part_and_stays_whole(string failure)
    {
        var (named, bare) = NodeFailureText.Split(failure);

        Assert.Null(named);
        Assert.Equal(failure, bare);
    }
}
