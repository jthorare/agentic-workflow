using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1b (messages spec §8.1): a Slack ts parses only in its one shape and orders by time, not by text.</summary>
[Lane(TestLane.Unit)]
public sealed class MessageTsTests
{
    [Fact]
    public void Parse_reads_seconds_a_dot_and_six_digits() =>
        Assert.Equal("1767776100.000100", MessageTs.Parse("1767776100.000100").Value);

    [Theory]
    [InlineData("")]
    [InlineData("1767776100")]
    [InlineData("1767776100.0001")]
    [InlineData("1767776100.0001000")]
    [InlineData(".000100")]
    [InlineData("-1767776100.000100")]
    [InlineData("17677a6100.000100")]
    [InlineData(" 1767776100.000100")]
    [InlineData("1767776100.000100\n")]
    [InlineData("1767776100.000100.000100")]
    [InlineData("1234567890123456.000100")]
    public void Parse_refuses_any_other_text(string text)
    {
        Assert.False(MessageTs.TryParse(text, out var ts));
        Assert.Equal(default, ts);

        // the refusal names the shape, never the text it was given
        Assert.Equal("A Slack ts is seconds, a dot and six digits.", Assert.Throws<FormatException>(() => MessageTs.Parse(text)).Message);
    }

    [Fact]
    public void TryParse_refuses_null() => Assert.False(MessageTs.TryParse(null, out _));

    [Fact]
    public void Compare_orders_by_the_seconds_as_a_number_not_as_text()
    {
        // as text "999999999.…" sorts after "1000000000.…"; as time it is older
        Assert.True(MessageTs.Compare(new MessageTs("999999999.999999"), new MessageTs("1000000000.000000")) < 0);
        Assert.True(MessageTs.Compare(new MessageTs("1000000000.000000"), new MessageTs("999999999.999999")) > 0);
        Assert.Equal(0, MessageTs.Compare(new MessageTs("01767776100.000100"), new MessageTs("1767776100.000100")));
    }

    [Fact]
    public void Compare_orders_microseconds_inside_one_second()
    {
        Assert.True(MessageTs.Compare(new MessageTs("1767776100.000100"), new MessageTs("1767776100.000200")) < 0);
        Assert.Equal(0, MessageTs.Compare(new MessageTs("1767776100.000100"), new MessageTs("1767776100.000100")));
    }

    [Fact]
    public void Compare_puts_a_malformed_ts_before_every_well_formed_one()
    {
        Assert.True(MessageTs.Compare(new MessageTs("not a ts"), new MessageTs("1.000000")) < 0);
        Assert.True(MessageTs.Compare(default, new MessageTs("1.000000")) < 0);
        Assert.True(MessageTs.Compare(new MessageTs("1.000000"), default) > 0);
        Assert.Equal(0, MessageTs.Compare(default, default));
    }

    [Fact]
    public void Compare_sorts_a_shuffled_list_into_time_order()
    {
        string[] times = ["1767776100.000100", "999999999.000001", "1767776100.000099", "1767771000.000100", "1767776640.000100"];

        var sorted = times.Select(static t => new MessageTs(t)).Order(Comparer<MessageTs>.Create(MessageTs.Compare)).Select(static t => t.Value);

        Assert.Equal(["999999999.000001", "1767771000.000100", "1767776100.000099", "1767776100.000100", "1767776640.000100"], sorted);
    }
}
