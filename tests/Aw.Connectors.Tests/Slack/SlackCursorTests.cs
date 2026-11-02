using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Tests.Slack;

public sealed class SlackCursorTests
{
    [Fact, Trait("Lane", "unit")]
    public void An_empty_cursor_has_read_nothing_and_has_no_text()
    {
        Assert.Null(SlackCursor.Empty.Newest("C0001GEN"));
        Assert.Equal(string.Empty, SlackCursor.Empty.ToText());
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_channel_keeps_its_own_watermark_and_the_text_round_trips_sorted()
    {
        var cursor = SlackCursor.Empty.With("G0002OPS", "1790000000.000200").With("C0001GEN", "1790000500.000100");

        Assert.Equal("C0001GEN=1790000500.000100,G0002OPS=1790000000.000200", cursor.ToText());
        var back = SlackCursor.Parse(cursor.ToText())!;
        Assert.Equal("1790000500.000100", back.Newest("C0001GEN"));
        Assert.Equal("1790000000.000200", back.Newest("G0002OPS"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_watermark_never_moves_backwards()
    {
        var cursor = SlackCursor.Empty.With("C0001GEN", "1790000500.000100").With("C0001GEN", "1790000100.000100");

        Assert.Equal("1790000500.000100", cursor.Newest("C0001GEN"));
    }

    [Fact, Trait("Lane", "unit")]
    public void With_leaves_the_old_cursor_alone()
    {
        var before = SlackCursor.Empty.With("C0001GEN", "1790000000.000100");

        _ = before.With("C0001GEN", "1790000900.000100");

        Assert.Equal("1790000000.000100", before.Newest("C0001GEN"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("nonsense")]
    [InlineData("C0001GEN")]
    [InlineData("C0001GEN=notats")]
    [InlineData("D0001DMX=1790000000.000100")]
    [InlineData("C0001GEN=1790000000.000100,")]
    public void Text_that_is_not_a_cursor_does_not_parse(string text) => Assert.Null(SlackCursor.Parse(text));

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    public void No_text_is_a_first_run(string? text) => Assert.Same(SlackCursor.Empty, SlackCursor.Parse(text));

    [Fact, Trait("Lane", "unit")]
    public void A_pass_in_progress_keeps_the_watermark_and_round_trips_in_the_short_text_form()
    {
        var cursor = SlackCursor.Empty
            .With("C0001GEN", "1790000000.000100")
            .WithPass("C0001GEN", new SlackPass("1790900000.000100", "1790800000.000000", "1790850000.000100"))
            .With("G0002OPS", "1790000000.000200");

        Assert.Equal("C0001GEN=1790000000.000100:1790900000.000100:1790800000.000000:1790850000.000100,G0002OPS=1790000000.000200", cursor.ToText());
        var back = SlackCursor.Parse(cursor.ToText())!;
        Assert.Equal("1790000000.000100", back.Newest("C0001GEN"));
        Assert.Equal(new SlackPass("1790900000.000100", "1790800000.000000", "1790850000.000100"), back.Pass("C0001GEN"));
        Assert.Null(back.Pass("G0002OPS"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pass_with_no_watermark_and_no_bounds_leaves_those_fields_empty()
    {
        var cursor = SlackCursor.Empty.WithPass("C0001GEN", new SlackPass("1790900000.000100", Below: null, ThreadsBelow: null));

        Assert.Equal("C0001GEN=:1790900000.000100::", cursor.ToText());
        var back = SlackCursor.Parse(cursor.ToText())!;
        Assert.Null(back.Newest("C0001GEN"));
        Assert.Equal(new SlackPass("1790900000.000100", Below: null, ThreadsBelow: null), back.Pass("C0001GEN"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Finishing_a_channel_with_a_watermark_ends_its_pass_and_never_moves_backwards()
    {
        var cursor = SlackCursor.Empty
            .With("C0001GEN", "1790900000.000100")
            .WithPass("C0001GEN", new SlackPass("1790950000.000100", Below: null, ThreadsBelow: null))
            .With("C0001GEN", "1790950000.000100");

        Assert.Equal("C0001GEN=1790950000.000100", cursor.ToText());
        Assert.Null(cursor.Pass("C0001GEN"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_old_text_form_still_parses_as_a_watermark_with_no_pass()
    {
        var back = SlackCursor.Parse("C0001GEN=1790000500.000100,G0002OPS=1790000000.000200")!;

        Assert.Equal("1790000500.000100", back.Newest("C0001GEN"));
        Assert.Null(back.Pass("C0001GEN"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("C0001GEN=1790000000.000100:notats::")]
    [InlineData("C0001GEN=:1790000000.000100:")]
    [InlineData("C0001GEN=:::")]
    [InlineData("C0001GEN=1790000000.000100:1790000000.000100:x:")]
    public void A_pass_that_is_malformed_does_not_parse(string text) => Assert.Null(SlackCursor.Parse(text));
}
