using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1b (messages spec §6.3, §11): what Mrkdwn parses each construct into, and that unbalanced, hostile or very long text never throws and comes back unchanged.</summary>
[Lane(TestLane.Unit)]
public sealed class MrkdwnParseTests
{
    [Fact]
    public void Marks_parse_to_their_own_spans()
    {
        var spans = Mrkdwn.Parse("*bold* and _italic_ and ~strike~").Spans;

        Assert.Equal([MrkdwnKind.Bold, MrkdwnKind.Text, MrkdwnKind.Italic, MrkdwnKind.Text, MrkdwnKind.Strike], spans.Select(static s => s.Kind));
        Assert.Equal(["bold", " and ", "italic", " and ", "strike"], spans.Select(static s => s.Text), StringComparer.Ordinal);
    }

    [Fact]
    public void Nested_marks_hold_their_inner_spans()
    {
        var bold = Assert.Single(Mrkdwn.Parse("*bold _and italic_*").Spans);

        Assert.Equal(MrkdwnKind.Bold, bold.Kind);
        Assert.Equal([MrkdwnKind.Text, MrkdwnKind.Italic], bold.Children.Select(static s => s.Kind));
        Assert.Equal("and italic", bold.Children[1].Text);

        var strike = Assert.Single(Assert.Single(Assert.Single(Mrkdwn.Parse("*_~deep~_*").Spans).Children).Children);
        Assert.Equal((MrkdwnKind.Strike, "deep"), (strike.Kind, strike.Text));
    }

    [Fact]
    public void Links_and_mentions_keep_their_target_and_label()
    {
        var link = Mrkdwn.Parse("see <https://github.com/acme/harbor/pull/3120|#3120>").Spans[1];
        var user = Mrkdwn.Parse("<@U0KIM> can you check?").Spans[0];
        var channel = Mrkdwn.Parse("in <#C0HARBOR|harbor> today").Spans[1];
        var here = Mrkdwn.Parse("<!here> lunch Friday?").Spans[0];

        Assert.Equal((MrkdwnKind.Link, "https://github.com/acme/harbor/pull/3120", "#3120", "#3120"), (link.Kind, link.Target, link.Label, link.Text));
        Assert.Equal((MrkdwnKind.UserMention, "U0KIM", null, "U0KIM"), (user.Kind, user.Target, user.Label, user.Text));
        Assert.Equal((MrkdwnKind.ChannelMention, "C0HARBOR", "harbor"), (channel.Kind, channel.Target, channel.Label));
        Assert.Equal((MrkdwnKind.Broadcast, "here"), (here.Kind, here.Target));
    }

    [Fact]
    public void Code_and_code_blocks_are_literal()
    {
        var code = Assert.Single(Mrkdwn.Parse("`*not bold*`").Spans);
        var block = Assert.Single(Mrkdwn.Parse("```\n*not bold*\n<@U0KIM>\n```").Spans);

        Assert.Equal((MrkdwnKind.Code, "*not bold*", 0), (code.Kind, code.Text, code.Children.Count));
        Assert.Equal((MrkdwnKind.Pre, "\n*not bold*\n<@U0KIM>\n", 0), (block.Kind, block.Text, block.Children.Count));
    }

    [Fact]
    public void Quote_and_list_lines_keep_their_marker_as_written()
    {
        var escaped = Assert.Single(Mrkdwn.Parse("&gt; quoted").Spans);
        var rest = Assert.Single(Mrkdwn.Parse(">>> all\nof this").Spans);
        var list = Mrkdwn.Parse("• one\n- two\n  1. three").Spans;

        Assert.Equal((MrkdwnKind.Quote, "&gt; ", "quoted"), (escaped.Kind, escaped.Marker, escaped.Text));
        Assert.Equal((MrkdwnKind.Quote, ">>> ", "all\nof this"), (rest.Kind, rest.Marker, rest.Text));
        Assert.Equal([MrkdwnKind.ListItem, MrkdwnKind.Text, MrkdwnKind.ListItem, MrkdwnKind.Text, MrkdwnKind.ListItem], list.Select(static s => s.Kind));
        Assert.Equal(["• ", "- ", "  1. "], list.Where(static s => s.Kind == MrkdwnKind.ListItem).Select(static s => s.Marker), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("*bold")]
    [InlineData("_a")]
    [InlineData("a * b * c")]
    [InlineData("~~")]
    [InlineData("`")]
    [InlineData("```\nopen")]
    [InlineData("<https://harbor.example")]
    [InlineData("<@>")]
    [InlineData("<<>>")]
    [InlineData("<javascript:alert(1)>")]
    [InlineData("<!subteam^S1|@team>")]
    [InlineData("<https://harbor.example/a b>")]
    [InlineData("foo_bar_baz")]
    [InlineData("at 10:30:45")]
    [InlineData(":not an emoji")]
    [InlineData("&lt &amp")]
    public void Unbalanced_or_unknown_markup_stays_text(string text)
    {
        var parsed = Mrkdwn.Parse(text);

        var only = Assert.Single(parsed.Spans);
        Assert.Equal((MrkdwnKind.Text, text), (only.Kind, only.Text));
        Assert.Equal(text, parsed.Render());
    }

    [Fact]
    public void Hostile_text_never_throws_and_comes_back_unchanged()
    {
        string[] hostile =
        [
            "‮evil‬", "a\u0000b\u0007c", "\uD800 lone \uDC00", "*_~`<>:&|", new string('<', 1000), new string('*', 5000),
            string.Concat(Enumerable.Repeat("*_~", 2000)), string.Concat(Enumerable.Repeat("<https://x.example|", 500)), "> > > >",
            "<https://x.example|<@U1>>", string.Concat(Enumerable.Repeat("```", 300)), string.Concat(Enumerable.Repeat("\n> ", 500)),
            string.Concat(Enumerable.Repeat(":a:", 3000)), "\r\n*bold*\r\n", "- \n* \n1. \n>",
        ];

        Assert.All(hostile, static text => Assert.Equal(text, Mrkdwn.Parse(text).Render()));
    }

    [Fact]
    public void Nesting_stops_at_the_depth_limit_and_the_rest_stays_text()
    {
        var text = string.Concat(Enumerable.Repeat(">>>\n", 30)) + "x";

        var parsed = Mrkdwn.Parse(text);

        // 8 quote levels (MrkdwnParser.MaxDepth), then the rest as one text span
        Assert.Equal(text, parsed.Render());
        Assert.Equal(9, Depth(parsed.Spans));
    }

    [Fact]
    public void A_very_long_line_parses_and_comes_back_unchanged()
    {
        var mixed = string.Concat(Enumerable.Repeat("*a _b ~c `d <e :f ", 3000));
        var marks = string.Concat(Enumerable.Repeat("*bold* ", 6000));

        Assert.Equal(mixed, Mrkdwn.Parse(mixed).Render());
        Assert.Equal(6000, Mrkdwn.Parse(marks).Spans.Count(static s => s.Kind == MrkdwnKind.Bold));
    }

    [Fact]
    public void Empty_text_has_no_spans() => Assert.Empty(Mrkdwn.Parse(string.Empty).Spans);

    [Fact]
    public void Display_reads_entities_and_drops_bidi_and_control_characters()
    {
        Assert.Equal("5 < 6 && 7 > 2", Mrkdwn.ToDisplay("5 &lt; 6 &amp;&amp; 7 &gt; 2"));
        Assert.Equal("&lt; once", Mrkdwn.ToDisplay("&amp;lt; once"));
        Assert.Equal("abc\td\nef", Mrkdwn.ToDisplay("a‮b\u0007c\td\ne⁦f"));
        Assert.Equal("a & b", Mrkdwn.Parse("<https://x.example|a &amp; b>").Spans[0].Display);
    }

    [Fact]
    public void Render_writes_hand_built_spans_with_default_marks()
    {
        MrkdwnSpan[] spans =
        [
            new(MrkdwnKind.Bold, "hi"), new(MrkdwnKind.Text, " and "), new(MrkdwnKind.Link, "https://x.example"), new(MrkdwnKind.Text, "\n"),
            new(MrkdwnKind.Quote, "said"), new(MrkdwnKind.Text, "\n"), new(MrkdwnKind.ListItem, "one"), new(MrkdwnKind.Text, " "), new(MrkdwnKind.Emoji, "tada"),
        ];

        Assert.Equal("*hi* and <https://x.example>\n> said\n• one :tada:", new Mrkdwn(spans).Render());
    }

    private static int Depth(IReadOnlyList<MrkdwnSpan> spans) => spans.Count == 0 ? 0 : spans.Max(static s => s.Children.Count == 0 ? 1 : 1 + Depth(s.Children));
}
