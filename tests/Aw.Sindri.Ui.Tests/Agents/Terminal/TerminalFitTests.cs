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
    [InlineData(5, 2)]
    public async Task A_size_of_zero_or_under_the_minimum_is_never_sent(int cols, int rows)
    {
        var socket = await Play(s => _ = s.FitSize(cols, rows, Token));

        Assert.Empty(socket.Sent);
    }
}
