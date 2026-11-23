using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Terminal;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>The size the pane fits goes to Serve as a `size` message, so tmux resizes and the text fills the pane (#3860).</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalFitTests
{
    private const string ReadyJson = "{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%0\"}}";
    private const string FrameJson = "{\"t\":\"frame\",\"seq\":1,\"rows\":[\"\"],\"cursor\":{\"x\":0,\"y\":0},\"w\":10,\"h\":1,\"alt\":0,\"inMode\":0,\"dead\":0}";

    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Plays ready and one frame; once the frame is drawn, `step` runs on the open socket (the pane fitting a new size).
    private static async Task<FakeTerminalConnector> Play(Action<TerminalSession> step, Action<TerminalSession>? before = null)
    {
        var f = new TerminalFixture();
        f.View.Open(TerminalFixture.Lane("ideas"), TerminalOpenIntent.Look);
        var socket = new FakeTerminalConnector { Script = [ReadyJson, FrameJson] };
        TerminalSession? session = null;
        session = new TerminalSession(socket, f.View, _ => step(session!));
        before?.Invoke(session);
        await session.Run(new TerminalOpen(_api, "claude-code", "ideas", 120, 36), Token);
        return socket;
    }

    [Fact]
    public async Task A_new_fitted_size_goes_out_as_one_size_message()
    {
        var socket = await Play(s => _ = s.FitSize(100, 50, Token));

        Assert.Equal([TerminalSocketMessages.Size(100, 50)], socket.Sent);
    }

    [Fact]
    public async Task The_same_size_twice_is_sent_once_even_when_it_equals_the_size_the_socket_opened_with()
    {
        var socket = await Play(s =>
        {
            _ = s.FitSize(120, 36, Token);
            _ = s.FitSize(120, 36, Token);
            _ = s.FitSize(100, 50, Token);
            _ = s.FitSize(100, 50, Token);
        });

        Assert.Equal([TerminalSocketMessages.Size(120, 36), TerminalSocketMessages.Size(100, 50)], socket.Sent);
    }

    // Serve ignores the hello's size and resizes only on a `size` message, so a fit that equals the open size still goes out once.
    [Fact]
    public async Task A_fit_equal_to_the_open_size_made_before_Run_is_sent_once_after_ready()
    {
        var socket = await Play(_ => { }, before: s => _ = s.FitSize(120, 36, Token));

        Assert.Equal([TerminalSocketMessages.Size(120, 36)], socket.Sent);
    }

    [Fact]
    public async Task With_no_fit_made_nothing_is_sent()
    {
        var socket = await Play(_ => { });

        Assert.Empty(socket.Sent);
    }

    [Fact]
    public async Task A_size_fitted_before_ready_waits_and_goes_out_once_after_it()
    {
        var socket = await Play(_ => { }, before: s =>
        {
            _ = s.FitSize(90, 40, Token);
            _ = s.FitSize(100, 50, Token);
        });

        Assert.Equal([TerminalSocketMessages.Size(100, 50)], socket.Sent);
    }

    [Theory]
    [InlineData(0, 36)]
    [InlineData(120, 0)]
    [InlineData(-1, 36)]
    public async Task A_size_of_zero_or_less_is_never_sent(int cols, int rows)
    {
        var socket = await Play(s => _ = s.FitSize(cols, rows, Token));

        Assert.Empty(socket.Sent);
    }

    // The server refuses under 20x5 and over 400x200 (TmuxTerminalPaneSource.Resize), so the client sends the nearest size it accepts.
    [Theory]
    [InlineData(5, 2, 20, 5)]
    [InlineData(10, 3, 20, 5)]
    [InlineData(19, 36, 20, 36)]
    [InlineData(120, 4, 120, 5)]
    [InlineData(500, 36, 400, 36)]
    [InlineData(120, 300, 120, 200)]
    public async Task A_size_outside_the_servers_bounds_is_sent_clamped_to_them(int cols, int rows, int sentCols, int sentRows)
    {
        var socket = await Play(s => _ = s.FitSize(cols, rows, Token));

        Assert.Equal([TerminalSocketMessages.Size(sentCols, sentRows)], socket.Sent);
    }

    [Fact]
    public async Task Two_fits_that_clamp_to_the_same_size_send_once()
    {
        var socket = await Play(s =>
        {
            _ = s.FitSize(10, 3, Token);
            _ = s.FitSize(15, 4, Token);
        });

        Assert.Equal([TerminalSocketMessages.Size(20, 5)], socket.Sent);
    }

    // The first send is in flight when the same size is fitted again: the second must see it as taken.
    [Fact]
    public async Task A_fit_landing_while_the_ready_send_is_in_flight_does_not_send_the_same_size_twice()
    {
        var f = new TerminalFixture();
        f.View.Open(TerminalFixture.Lane("ideas"), TerminalOpenIntent.Look);
        var socket = new FakeTerminalConnector { Script = [ReadyJson, FrameJson] };
        var session = new TerminalSession(socket, f.View, _ => { });
        var landed = false;
        socket.BeforeSend = async _ =>
        {
            if (!landed)
            {
                landed = true;
                _ = await session.FitSize(100, 50, Token);
            }
        };
        _ = session.FitSize(100, 50, Token);

        await session.Run(new TerminalOpen(_api, "claude-code", "ideas", 120, 36), Token);

        Assert.Equal([TerminalSocketMessages.Size(100, 50)], socket.Sent);
    }
}
