using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Terminal;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Terminal;

/// <summary>The terminal WebSocket's messages (docs/sindri/agents.md): what the window sends and how it reads what Serve sends.</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalSocketMessagesTests
{
    private static string Field(string json, string name) => JsonDocument.Parse(json).RootElement.GetProperty(name).ToString();

    [Fact]
    public void Hello_carries_the_token_version_and_size_in_the_first_message()
    {
        var json = TerminalSocketMessages.Hello("tok", 132, 44);

        Assert.Equal(("hello", "1", "tok", "132", "44"), (Field(json, "t"), Field(json, "v"), Field(json, "token"), Field(json, "cols"), Field(json, "rows")));
    }

    [Fact]
    public void Key_text_paste_size_and_pong_have_the_shapes_of_the_table()
    {
        Assert.Equal(("key", "Enter"), (Field(TerminalSocketMessages.Key("Enter"), "t"), Field(TerminalSocketMessages.Key("Enter"), "key")));
        Assert.Equal(("key", "a b"), (Field(TerminalSocketMessages.Text("a b"), "t"), Field(TerminalSocketMessages.Text("a b"), "text")));
        Assert.Equal(("paste", "x"), (Field(TerminalSocketMessages.Paste("x"), "t"), Field(TerminalSocketMessages.Paste("x"), "text")));
        Assert.Equal(("size", "100", "30"), (Field(TerminalSocketMessages.Size(100, 30), "t"), Field(TerminalSocketMessages.Size(100, 30), "cols"), Field(TerminalSocketMessages.Size(100, 30), "rows")));
        Assert.Equal("pong", Field(TerminalSocketMessages.Pong(), "t"));
    }

    [Fact]
    public void Interrupt_is_the_named_key_C_c_so_it_needs_no_route() =>
        Assert.Equal(TerminalSocketMessages.Key("C-c"), TerminalSocketMessages.Interrupt());

    [Theory]
    [InlineData("Enter", null, "key", "Enter")]
    [InlineData("Esc", null, "key", "Escape")]
    [InlineData("Back", null, "key", "BSpace")]
    [InlineData("Delete", null, "key", "DC")]
    [InlineData("Shift+Tab", null, "key", "BTab")]
    [InlineData("Next", null, "key", "PageDown")]
    [InlineData("Ctrl+C", null, "key", "C-c")]
    [InlineData("Alt+B", null, "key", "M-b")]
    [InlineData("F5", null, "key", "F5")]
    [InlineData("Up", null, "key", "Up")]
    public void A_key_the_view_saw_becomes_a_named_key(string name, string? text, string kind, string sent)
    {
        var json = TerminalSocketMessages.FromKey(name, text);

        Assert.NotNull(json);
        Assert.Equal((kind, sent), (Field(json, "t"), Field(json, "key")));
    }

    [Fact]
    public void A_key_that_typed_text_sends_the_text_and_one_with_neither_name_nor_text_is_dropped()
    {
        Assert.Equal("é", Field(TerminalSocketMessages.FromKey("E", "é")!, "text"));
        Assert.Null(TerminalSocketMessages.FromKey("Capital", null));
    }

    [Fact]
    public void Parse_reads_ready_ping_and_unknown()
    {
        var ready = TerminalSocketMessages.Parse("{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"fx\",\"pane\":\"%0\"}}");
        var ping = TerminalSocketMessages.Parse("{\"t\":\"ping\"}");
        var other = TerminalSocketMessages.Parse("{\"t\":\"later\"}");
        var broken = TerminalSocketMessages.Parse("not json");

        Assert.Equal((TerminalServerKind.Ready, TerminalServerKind.Ping, TerminalServerKind.Unknown, TerminalServerKind.Unknown), (ready.Kind, ping.Kind, other.Kind, broken.Kind));
    }

    [Fact]
    public void Parse_reads_every_frame_of_the_fixture_with_its_size_cursor_and_flags()
    {
        var frames = FakeTerminalConnector.FixtureOut().Select(TerminalSocketMessages.Parse).Where(m => m.Kind == TerminalServerKind.Frame).ToList();

        Assert.True(frames.Count > 1);
        Assert.All(frames, f => Assert.Equal((120, 36, false), (f.Status.Width, f.Status.Height, f.Status.Dead)));
        Assert.Equal(Enumerable.Range(1, frames.Count), frames.Select(f => f.Seq));
        Assert.Equal(36, frames[0].Rows.Count);
    }

    [Theory]
    [InlineData("""{"t":"frame"}""")]
    [InlineData("""{"t":"frame","rows":["a"],"w":1,"h":1}""")]
    [InlineData("""{"t":"frame","rows":"x","cursor":{"x":0,"y":0},"w":1,"h":1,"inMode":0,"alt":0,"dead":0,"seq":1}""")]
    [InlineData("""{"t":"frame","rows":[],"cursor":{"x":"a","y":0},"w":1,"h":1,"inMode":0,"alt":0,"dead":0,"seq":1}""")]
    public void A_frame_with_a_missing_or_wrong_property_is_Unknown_not_a_throw(string json) =>
        Assert.Equal(TerminalServerKind.Unknown, TerminalSocketMessages.Parse(json).Kind);
}
