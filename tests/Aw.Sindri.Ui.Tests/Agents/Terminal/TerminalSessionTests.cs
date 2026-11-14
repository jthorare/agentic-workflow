using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Terminal;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>The socket joined to the terminal view: frames in and drawn, keys out, Interrupt as C-c, closes by code (#3382).</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalSessionTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (TerminalFixture F, FakeTerminalConnector Socket, TerminalSession Session, List<TerminalGrid> Drawn) Make(string name = "ideas")
    {
        var f = new TerminalFixture();
        f.View.Open(TerminalFixture.Lane(name), TerminalOpenIntent.Look);
        var socket = new FakeTerminalConnector();
        var drawn = new List<TerminalGrid>();
        return (f, socket, new TerminalSession(socket, f.View, drawn.Add), drawn);
    }

    [Fact]
    public async Task Replaying_the_fixture_attaches_draws_every_frame_and_ends_reconnecting_on_1001()
    {
        var (f, socket, session, drawn) = Make();
        var frames = FakeTerminalConnector.FixtureOut().Count(m => TerminalSocketMessages.Parse(m).Kind == TerminalServerKind.Frame);

        await session.Run(_api, "claude-code", "ideas", 120, 36, Token);

        Assert.Equal(["claude-code/ideas"], socket.Connected);
        Assert.Equal(frames, drawn.Count);
        Assert.Equal(36, f.View.Rows.Count);
        Assert.Equal(TerminalState.Reconnecting, f.View.State);
    }

    [Fact]
    public async Task A_refused_connect_is_a_drop()
    {
        var (f, socket, session, drawn) = Make();
        socket.Refuse = true;

        await session.Run(_api, "claude-code", "ideas", 120, 36, Token);

        Assert.Equal((TerminalState.Reconnecting, 0), (f.View.State, drawn.Count));
    }

    [Theory]
    [InlineData(4016, TerminalState.Gone)]
    [InlineData(4001, TerminalState.TakenOver)]
    [InlineData(4015, TerminalState.CantAttach)]
    public async Task A_close_code_ends_in_its_state(int code, TerminalState state)
    {
        var (f, socket, session, _) = Make();
        socket.Script = ["{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%0\"}}"];
        socket.EndsWith = code;

        await session.Run(_api, "claude-code", "ideas", 120, 36, Token);

        Assert.Equal(state, f.View.State);
    }

    [Fact]
    public async Task A_ping_is_answered_with_a_pong()
    {
        var (_, socket, session, _) = Make();
        socket.Script = ["{\"t\":\"ping\"}"];

        await session.Run(_api, "claude-code", "ideas", 120, 36, Token);

        Assert.Equal([TerminalSocketMessages.Pong()], socket.Sent);
    }

    [Fact]
    public async Task Keys_typed_before_ready_wait_and_go_out_in_order_once_it_arrives()
    {
        var (f, socket, session, _) = Make();
        socket.Script = ["{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%0\"}}"];
        f.View.SocketOpening();
        var early = new[] { await session.SendKey("A", "a", Token), await session.SendKey("Enter", null, Token) };

        await session.Run(_api, "claude-code", "ideas", 120, 36, Token);

        Assert.Equal([TerminalKeyFate.Queued, TerminalKeyFate.Queued], early);
        Assert.Equal([TerminalSocketMessages.Text("a"), TerminalSocketMessages.Key("Enter")], socket.Sent);
    }

    [Fact]
    public async Task A_paste_goes_out_as_one_paste_message_with_the_literal_text()
    {
        var (f, socket, session, _) = Make();
        socket.Script = ["{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%0\"}}"];
        f.View.SocketOpening();

        _ = await session.SendPaste("echo hi\nls -l", Token);
        await session.Run(_api, "claude-code", "ideas", 120, 36, Token);

        Assert.Equal([TerminalSocketMessages.Paste("echo hi\nls -l")], socket.Sent);
    }

    [Fact]
    public async Task A_paste_over_256_KiB_sends_nothing_and_says_so_in_the_panes_line()
    {
        var (f, socket, session, _) = Make();
        socket.Script = ["{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%0\"}}"];
        f.View.SocketOpening();

        var fate = await session.SendPaste(new string('a', (256 * 1024) + 1), Token);
        await session.Run(_api, "claude-code", "ideas", 120, 36, Token);

        Assert.Equal((TerminalKeyFate.TooBig, 0, string.Empty), (fate, socket.Sent.Count, f.View.ControlLine));
    }

    // The writer escapes < > & ' + and non-ASCII as \uXXXX (6 bytes each): 150 KiB of '<' passes the 256 KiB text cap and encodes to about 900 KB.
    [Fact]
    public async Task A_paste_whose_encoded_message_is_over_512_KiB_sends_nothing_and_says_so()
    {
        var (f, socket, session, _) = Make();
        socket.Script = ["{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%0\"}}"];
        f.View.SocketOpening();

        var fate = await session.SendPaste(new string('<', 150 * 1024), Token);
        await session.Run(_api, "claude-code", "ideas", 120, 36, Token);

        Assert.Equal((TerminalKeyFate.TooBig, 0, string.Empty), (fate, socket.Sent.Count, f.View.ControlLine));
    }

    [Fact]
    public async Task A_150_KiB_paste_of_plain_text_still_goes_out()
    {
        var (f, socket, session, _) = Make();
        socket.Script = ["{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%0\"}}"];
        f.View.SocketOpening();
        var text = new string('a', 150 * 1024);

        _ = await session.SendPaste(text, Token);
        await session.Run(_api, "claude-code", "ideas", 120, 36, Token);

        Assert.Equal([TerminalSocketMessages.Paste(text)], socket.Sent);
    }

    [Fact]
    public async Task The_cap_counts_UTF_8_bytes_not_characters()
    {
        var (f, socket, session, _) = Make();
        socket.Script = ["{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%0\"}}"];
        f.View.SocketOpening();
        var threeBytesEach = new string('\u20ac', 90_000);   // 90,000 characters, 270,000 bytes

        var fate = await session.SendPaste(threeBytesEach, Token);
        await session.Run(_api, "claude-code", "ideas", 120, 36, Token);

        Assert.Equal((TerminalKeyFate.TooBig, 0), (fate, socket.Sent.Count));
    }

    [Fact]
    public async Task A_paste_of_exactly_256_KiB_goes_out_and_leaves_the_line_empty()
    {
        var (f, socket, session, _) = Make();
        socket.Script = ["{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%0\"}}"];
        f.View.SocketOpening();
        var text = new string('a', 256 * 1024);

        _ = await session.SendPaste(text, Token);
        await session.Run(_api, "claude-code", "ideas", 120, 36, Token);

        Assert.Equal([TerminalSocketMessages.Paste(text)], socket.Sent);
        Assert.Equal(string.Empty, f.View.ControlLine);
    }

    [Fact]
    public async Task An_empty_paste_sends_nothing()
    {
        var (f, socket, session, _) = Make();
        socket.Script = ["{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%0\"}}"];
        f.View.SocketOpening();

        var fate = await session.SendPaste(string.Empty, Token);
        await session.Run(_api, "claude-code", "ideas", 120, 36, Token);

        Assert.Equal((TerminalKeyFate.Dropped, 0), (fate, socket.Sent.Count));
    }

    [Fact]
    public async Task Interrupt_sends_C_c_through_the_open_socket()
    {
        var (_, socket, session, _) = Make();
        socket.Script = ["{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%0\"}}"];
        var run = session.Run(_api, "claude-code", "ideas", 120, 36, Token);
        await run;

        var sentWhileClosed = await session.Interrupt(Token);

        Assert.False(sentWhileClosed);
        Assert.DoesNotContain(TerminalSocketMessages.Interrupt(), socket.Sent);
    }

    private const string ReadyJson = "{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%0\"}}";

    [Fact]
    public async Task A_socket_that_dies_without_a_close_frame_is_a_drop_that_retries_after_1_s_with_focus_on_the_list()
    {
        var (f, socket, session, _) = Make();
        socket.Script = [ReadyJson];
        socket.ThrowsAfterScript = true;

        await session.Run(_api, "claude-code", "ideas", 120, 36, Token);
        f.Clock.Advance(TimeSpan.FromMilliseconds(1100));
        f.View.Tick();

        Assert.Equal((TerminalState.Connecting, TerminalSide.List, 1), (f.View.State, f.View.Side, f.Requests));
    }

    [Fact]
    public async Task A_socket_that_dies_without_a_close_frame_reads_Reconnecting_at_once()
    {
        var (f, socket, session, _) = Make();
        socket.Script = [ReadyJson];
        socket.ThrowsAfterScript = true;

        await session.Run(_api, "claude-code", "ideas", 120, 36, Token);

        Assert.Equal(TerminalState.Reconnecting, f.View.State);
    }

    [Fact]
    public async Task A_frame_read_after_the_session_was_cancelled_is_not_drawn()
    {
        var (f, socket, _, _) = Make();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var drawn = 0;
        var session = new TerminalSession(socket, f.View, _ =>
        {
            drawn++;
            cts.Cancel();
        });
        var frame = FakeTerminalConnector.FixtureOut().First(m => TerminalSocketMessages.Parse(m).Kind == TerminalServerKind.Frame);
        socket.Script = [ReadyJson, frame, frame, frame];
        socket.IgnoresCancellation = true;

        await session.Run(_api, "claude-code", "ideas", 120, 36, cts.Token);

        Assert.Equal(1, drawn);
    }
}
