using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>
/// A scrolled-back view of a real tmux pane whose history is full (issue 3829): tmux drops the oldest rows as new ones arrive, so the history size stops
/// counting them, and the view still shows the same rows. Heavy: it starts tmux.
/// </summary>
[Lane(TestLane.Heavy)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class TmuxScrollbackLimitTests : IDisposable
{
    private const string Session = "sindri-abcd1234";
    private const int Limit = 50;
    private readonly PrivateTmux _tmux = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _tmux.Dispose();

    [Fact]
    public async Task New_output_at_the_history_limit_keeps_the_scrolled_view_on_its_rows()
    {
        var source = await Start(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"seq 1 {Limit * 3}; printf 'ready> '; cat"));
        await using var stream = (await source.Open(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), size: null, Token)).Value;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        stop.CancelAfter(TimeSpan.FromSeconds(10));
        await using var frames = stream.Frames(stop.Token).GetAsyncEnumerator(stop.Token);
        _ = await Next(frames, f => f.Hist >= Limit - (Limit / 10));   // full: tmux keeps it between 45 and 50 rows

        Expect.True((await stream.Scroll(20, Token)).IsSuccess);
        var before = await Next(frames, f => f.Back == 20);

        // Four typed lines are eight new rows: more than the five rows tmux trims at once, so the history size can't count them.
        for (var i = 0; i < 4; i++)
        {
            Assert.NotNull(_tmux.Run("send-keys", "-t", Session, "-l", "--", "more"));
            Assert.NotNull(_tmux.Run("send-keys", "-t", Session, "Enter"));
        }

        var moved = await Next(frames, f => f.Back > before.Back);
        var seen = await Window(frames, stop, TimeSpan.FromSeconds(1));
        seen.Insert(0, moved);

        Expect.True(seen.TrueForAll(f => f.Rows.SequenceEqual(before.Rows, StringComparer.Ordinal)));
        Expect.Equal(before.Back + 8, seen[^1].Back);
    }

    private async ValueTask<ITerminalPaneSource> Start(string shell)
    {
        // history-limit applies to panes made after it is set, so the server starts with a spare session and the option first.
        Assert.NotNull(_tmux.Run("new-session", "-d", "-s", "spare", "sleep", "600"));
        Assert.NotNull(_tmux.Run("set-option", "-g", "history-limit", Limit.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var host = new TmuxSessionHost(_tmux.Options);
        var session = new HostSession(Session, _tmux.Scratch, ["sh", "-c", shell], new Dictionary<string, string>(StringComparer.Ordinal))
        {
            Size = new HostSize(80, 12),
        };
        Assert.True((await host.Start(session, TimeSpan.FromSeconds(4), CancellationToken.None)).IsSuccess);
        return new TmuxTerminalPaneSource(_tmux.Options, new OneTarget(new TerminalTarget(_tmux.Options.Socket, SocketIsPath: false, Session, Session, Foreign: false)));
    }

    private static async Task<TerminalFrame> Next(IAsyncEnumerator<TerminalFrame> frames, Func<TerminalFrame, bool> wanted)
    {
        while (await frames.MoveNextAsync())
        {
            if (wanted(frames.Current))
            {
                return frames.Current;
            }
        }

        throw new InvalidOperationException("the frames ended before one matched");
    }

    private static async Task<List<TerminalFrame>> Window(IAsyncEnumerator<TerminalFrame> frames, CancellationTokenSource stop, TimeSpan span)
    {
        var seen = new List<TerminalFrame>();
        stop.CancelAfter(span);
        try
        {
            while (await frames.MoveNextAsync())
            {
                seen.Add(frames.Current);
            }
        }
        catch (OperationCanceledException)
        {
            // the window ended
        }

        return seen;
    }

    private sealed class OneTarget(Result<TerminalTarget> answer) : ITerminalPaneLocator
    {
        public ValueTask<Result<TerminalTarget>> Find(SessionRef session, CancellationToken cancellationToken) => ValueTask.FromResult(answer);
    }
}
