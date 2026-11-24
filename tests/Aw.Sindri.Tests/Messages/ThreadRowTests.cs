using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1b (messages spec §5.3, §7.3): row keys in Home's '&lt;source&gt;:&lt;kind&gt;:&lt;source id&gt;' shape, and the gap marker's line.</summary>
[Lane(TestLane.Unit)]
public sealed class ThreadRowTests
{
    [Fact]
    public void Row_keys_follow_the_home_shape()
    {
        Assert.Equal("slack:dm:T0HARBOR/D0SAM", ThreadRow.DmKey("T0HARBOR", "D0SAM"));
        Assert.Equal("slack:thread:T0HARBOR/C0HARBOR/1767771000.000100", ThreadRow.ThreadKey("T0HARBOR", "C0HARBOR", new MessageTs("1767771000.000100")));
    }

    [Theory]
    [InlineData("", "D0SAM")]
    [InlineData("T0HARBOR", " ")]
    [InlineData("T0/HARBOR", "D0SAM")]
    [InlineData("T0HARBOR", "D0/SAM")]
    public void A_key_part_that_is_empty_or_holds_a_slash_is_refused(string workspace, string conversation)
    {
        _ = Assert.Throws<ArgumentException>(() => ThreadRow.DmKey(workspace, conversation));
        _ = Assert.Throws<ArgumentException>(() => ThreadRow.ThreadKey(workspace, conversation, new MessageTs("1767771000.000100")));
    }

    [Fact]
    public void A_thread_key_refuses_a_thread_that_is_not_a_ts() =>
        Assert.Throws<ArgumentException>(() => ThreadRow.ThreadKey("T0HARBOR", "C0HARBOR", new MessageTs("1767771000")));

    [Theory]
    [InlineData(12, "12 older messages not loaded. Load older (1 per minute)")]
    [InlineData(1, "1 older message not loaded. Load older (1 per minute)")]
    [InlineData(0, "Older messages not loaded. Load older (1 per minute)")]
    [InlineData(-3, "Older messages not loaded. Load older (1 per minute)")]
    [InlineData(12000, "12000 older messages not loaded. Load older (1 per minute)")]
    public void Gap_marker_line_says_how_many_messages_are_missing(int missing, string line) =>
        Assert.Equal(line, new GapMarker("C0HARBOR", new MessageTs("1767771000.000100"), new MessageTs("1767776100.000100"), missing).Line());
}
