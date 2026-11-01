using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Tests.Slack;

// Issue #1689: the per-channel replies mark rides on the cursor text as '~mark'.
public sealed class SlackCursorMarkTests
{
    private const string Watermark = "1700000000.000100";
    private const string Mark = "1700000500.000200";

    [Fact, Trait("Lane", "unit")]
    public void The_mark_round_trips_with_and_without_a_pass()
    {
        var plain = SlackCursor.Empty.With("C0001GEN", Watermark).WithRepliesMark("C0001GEN", Mark);
        Assert.Equal("C0001GEN=" + Watermark + "~" + Mark, plain.ToText());
        Assert.Equal(plain.ToText(), SlackCursor.Parse(plain.ToText())!.ToText());

        var pass = plain.WithPass("C0001GEN", new SlackPass("1700000900.000100", Below: null, ThreadsBelow: null));
        Assert.EndsWith("~" + Mark, pass.ToText(), StringComparison.Ordinal);
        Assert.Equal(pass.ToText(), SlackCursor.Parse(pass.ToText())!.ToText());
        Assert.Equal(Mark, SlackCursor.Parse(pass.ToText())!.RepliesMark("C0001GEN"));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_old_text_without_a_mark_reads_the_watermark_as_the_mark()
    {
        var cursor = SlackCursor.Parse("C0001GEN=" + Watermark)!;

        Assert.Equal(Watermark, cursor.RepliesMark("C0001GEN"));
        Assert.Null(cursor.RepliesMark("C0002OTH"));
        Assert.Equal("C0001GEN=" + Watermark, cursor.ToText());
    }

    [Fact, Trait("Lane", "unit")]
    public void The_mark_never_moves_backwards_and_survives_a_watermark_move()
    {
        var cursor = SlackCursor.Empty.With("C0001GEN", Watermark).WithRepliesMark("C0001GEN", Mark).WithRepliesMark("C0001GEN", "1700000100.000000");

        Assert.Equal(Mark, cursor.RepliesMark("C0001GEN"));
        Assert.Equal(Mark, cursor.With("C0001GEN", "1700001000.000000").RepliesMark("C0001GEN"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("C0001GEN=1700000000.000100~nope")]
    [InlineData("C0001GEN=1700000000.000100~1700000500.000200~1700000600.000200")]
    public void A_bad_mark_is_not_a_cursor(string text) => Assert.Null(SlackCursor.Parse(text));
}
