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
}
