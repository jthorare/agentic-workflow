using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="ITerminalPaneSource"/> honours (agents spec §15.7 and §15.8). Run against the fake (fixture frames) and, in the heavy lane,
/// the tmux adapter on a private tmux server, so the real source cannot drift from the fake.
/// </summary>
public abstract class TerminalPaneSourceContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A source whose session 'live' shows a prompt and echoes what is typed.</summary>
    /// <returns>The source.</returns>
    protected abstract ValueTask<ITerminalPaneSource> Create();

    /// <summary>A source whose session 'live' has at least <paramref name="lines"/> rows of history above its screen (numbered rows, then a prompt).</summary>
    /// <param name="lines">The rows of history.</param>
    /// <returns>The source.</returns>
    protected abstract ValueTask<ITerminalPaneSource> CreateWithHistory(int lines);

    /// <summary>Makes the pane of <see cref="CreateWithHistory"/> print at least <paramref name="lines"/> more rows, as an agent's output does.</summary>
    /// <param name="source">The source <see cref="CreateWithHistory"/> returned.</param>
    /// <param name="lines">The rows.</param>
    /// <returns>A task that ends when the output is sent.</returns>
    protected abstract ValueTask GrowHistory(ITerminalPaneSource source, int lines);

    /// <summary>Makes the pane of <see cref="CreateWithHistory"/> switch to the alternate screen, as vim does.</summary>
    /// <param name="source">The source <see cref="CreateWithHistory"/> returned.</param>
    /// <returns>A task that ends when the switch is sent.</returns>
    protected abstract ValueTask EnterAlternateScreen(ITerminalPaneSource source);

    /// <summary>A source whose session 'live' is on the alternate screen, with history behind it on the normal screen.</summary>
    /// <returns>The source.</returns>
    protected abstract ValueTask<ITerminalPaneSource> CreateOnAlternateScreen();

    /// <summary>A source that answers <paramref name="code"/> to every open (SND-AGENT-015: no pane; SND-AGENT-016: tmux or the session is gone).</summary>
    /// <param name="code">The error code.</param>
    /// <returns>The source.</returns>
    protected abstract ValueTask<ITerminalPaneSource> CreateRefusing(string code);

    /// <summary>The first frame arrives without a key, numbered from 1, and names the pane.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_open_stream_sends_the_screen_first()
    {
        var source = await Create();
        await using var stream = (await source.Open(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), size: null, Token)).Value;

        var first = await FirstFrame(stream);

        Expect.Equal(1, first.Seq);
        Expect.True(!string.IsNullOrEmpty(stream.Host.Pane));
        Expect.True(first.Width > 0 && first.Height > 0);
    }

    /// <summary>A key is followed by a frame without waiting for a tick (perf D14), and frames count up.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_key_makes_a_frame_at_once()
    {
        var source = await Create();
        await using var stream = (await source.Open(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), size: null, Token)).Value;
        var first = await FirstFrame(stream);

        Expect.True((await stream.SendKey("Enter", Token)).IsSuccess);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromMilliseconds(25));

        await using var frames = stream.Frames(limit.Token).GetAsyncEnumerator(limit.Token);
        Expect.True(await frames.MoveNextAsync());
        Expect.True(frames.Current.Seq > first.Seq);
    }

    /// <summary>A scroll shows the rows above the screen: the frame is the live frame shifted down by the offset, with the offset and the history size named.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Scroll_back_captures_history_rows_with_S_and_E()
    {
        var source = await CreateWithHistory(100);
        await using var stream = await OpenLive(source);
        await using var frames = Frames(stream);
        var live = await frames.Next(f => f.Hist >= 100);
        Expect.Equal(0, live.Back);

        Expect.True((await stream.Scroll(5, Token)).IsSuccess);
        var scrolled = await frames.Next(f => f.Back == 5);

        Expect.Equal(live.Height, scrolled.Rows.Count);
        Expect.True(scrolled.Rows.Take(5).All(r => r.Length > 0));   // five rows of history on top
        Expect.True(live.Rows.Take(live.Height - 5).SequenceEqual(scrolled.Rows.Skip(5), StringComparer.Ordinal));   // the old screen, five rows lower
    }

    /// <summary>An offset past the history is clamped to the history size, and a negative one is the live screen.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Scroll_is_clamped_to_history_size()
    {
        var source = await CreateWithHistory(100);
        await using var stream = await OpenLive(source);
        await using var frames = Frames(stream);
        var live = await frames.Next(f => f.Hist >= 100);

        Expect.True((await stream.Scroll(1_000_000, Token)).IsSuccess);
        var top = await frames.Next(f => f.Back > 0);
        Expect.Equal(top.Hist, top.Back);
        Expect.True(top.Back >= live.Hist);

        Expect.True((await stream.Scroll(-3, Token)).IsSuccess);
        Expect.Equal(0, (await frames.Next(f => f.Back == 0)).Back);
    }

    /// <summary>A scroll to 0 shows the live screen again.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Scroll_to_zero_returns_to_the_live_screen()
    {
        var source = await CreateWithHistory(100);
        await using var stream = await OpenLive(source);
        await using var frames = Frames(stream);
        var live = await frames.Next(f => f.Hist >= 100);
        Expect.True((await stream.Scroll(7, Token)).IsSuccess);
        _ = await frames.Next(f => f.Back == 7);

        Expect.True((await stream.Scroll(0, Token)).IsSuccess);
        var back = await frames.Next(f => f.Back == 0);

        Expect.True(live.Rows.SequenceEqual(back.Rows, StringComparer.Ordinal));
    }

    /// <summary>A key, typed text and a paste each return the view to the live screen.</summary>
    /// <param name="input">'key', 'text' or 'paste'.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData("key")]
    [InlineData("text")]
    [InlineData("paste")]
    public async Task Key_input_returns_the_view_to_live(string input)
    {
        var source = await CreateWithHistory(100);
        await using var stream = await OpenLive(source);
        await using var frames = Frames(stream);
        _ = await frames.Next(f => f.Hist >= 100);
        Expect.True((await stream.Scroll(5, Token)).IsSuccess);
        _ = await frames.Next(f => f.Back == 5);

        var sent = input switch
        {
            "key" => await stream.SendKey("Enter", Token),
            "text" => await stream.SendText("x", Token),
            _ => await stream.Paste("x", Token),
        };

        Expect.True(sent.IsSuccess);
        Expect.Equal(0, (await frames.Next(f => f.Back == 0)).Back);
    }

    /// <summary>New output while the view is scrolled moves the offset with it, so the view shows the same lines.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task New_output_while_scrolled_keeps_the_view_anchored()
    {
        var source = await CreateWithHistory(100);
        await using var stream = await OpenLive(source);
        await using var frames = Frames(stream);
        _ = await frames.Next(f => f.Hist >= 100);
        Expect.True((await stream.Scroll(40, Token)).IsSuccess);
        var before = await frames.Next(f => f.Back == 40);

        await GrowHistory(source, 10);
        var after = await frames.Next(f => f.Hist > before.Hist);

        Expect.Equal(40 + (after.Hist - before.Hist), after.Back);
        Expect.True(before.Rows.SequenceEqual(after.Rows, StringComparer.Ordinal));
    }

    /// <summary>A size message keeps the offset (re-clamped to the history), never the live screen.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_resize_keeps_the_scroll_offset()
    {
        var source = await CreateWithHistory(100);
        await using var stream = await OpenLive(source);
        await using var frames = Frames(stream);
        _ = await frames.Next(f => f.Hist >= 100);
        Expect.True((await stream.Scroll(5, Token)).IsSuccess);
        _ = await frames.Next(f => f.Back == 5);

        Expect.True((await stream.Resize(new HostSize(120, 30), Token)).IsSuccess);
        var seen = await frames.Window(TimeSpan.FromMilliseconds(300));

        Expect.True(seen.TrueForAll(f => f.Back > 0));
    }

    /// <summary>The alternate screen has no scrollback: its frames say hist 0 and a scroll stays on the live screen.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_alternate_screen_has_no_scrollback()
    {
        var source = await CreateOnAlternateScreen();
        await using var stream = await OpenLive(source);
        await using var frames = Frames(stream);
        var alt = await frames.Next(f => f.Alt);
        Expect.Equal(0, alt.Hist);

        Expect.True((await stream.Scroll(5, Token)).IsSuccess);
        var seen = await frames.Window(TimeSpan.FromMilliseconds(300));

        Expect.True(seen.TrueForAll(f => f.Back == 0 && f.Hist == 0));
    }

    /// <summary>The app enters the alternate screen while the view is scrolled: the first alt frame says back 0, and the old offset does not come back after.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Entering_alt_screen_drops_the_scroll_back_to_live()
    {
        var source = await CreateWithHistory(100);
        await using var stream = await OpenLive(source);
        await using var frames = Frames(stream);
        _ = await frames.Next(f => f.Hist >= 100);
        Expect.True((await stream.Scroll(5, Token)).IsSuccess);
        _ = await frames.Next(f => f.Back == 5);

        await EnterAlternateScreen(source);
        var alt = await frames.Next(f => f.Alt);

        Expect.Equal(0, alt.Back);
        Expect.Equal(0, alt.Hist);
    }

    /// <summary>A key name outside the list is refused and nothing reaches the pane.</summary>
    /// <param name="key">The key name.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData("F13")]
    [InlineData("C-1")]
    [InlineData("M-ab")]
    [InlineData("Foo")]
    [InlineData("")]
    public async Task A_key_outside_the_list_is_refused(string key)
    {
        var source = await Create();
        await using var stream = (await source.Open(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), size: null, Token)).Value;

        var sent = await stream.SendKey(key, Token);

        Expect.Equal(SindriErrors.InvalidInput.Code, sent.Error.Code);
    }

    /// <summary>Text over 4 KiB, a paste over 256 KiB and a size out of range are refused.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Input_over_its_limit_is_refused()
    {
        var source = await Create();
        await using var stream = (await source.Open(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), size: null, Token)).Value;

        Expect.True((await stream.SendText(new string('a', 4097), Token)).IsError);
        Expect.True((await stream.Paste(new string('a', (256 * 1024) + 1), Token)).IsError);
        Expect.True((await stream.Resize(new HostSize(19, 36), Token)).IsError);
        Expect.True((await stream.Resize(new HostSize(120, 201), Token)).IsError);
        Expect.True((await stream.Resize(new HostSize(120, 36), Token)).IsSuccess);
    }

    /// <summary>Typed text arrives whole, a trailing ';' included (tmux reads a trailing ';' in an argument as a command separator and drops it).</summary>
    /// <param name="before">Text typed first in its own message, or empty.</param>
    /// <param name="text">The text typed in one message.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData("", "ab;")]
    [InlineData("ab", ";")]
    [InlineData("", "ab;;")]
    [InlineData("", "x\\;")]
    public async Task Typed_text_arrives_with_its_semicolons(string before, string text)
    {
        var source = await Create();
        await using var stream = (await source.Open(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), size: null, Token)).Value;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(5));
        await using var frames = stream.Frames(limit.Token).GetAsyncEnumerator(limit.Token);
        Expect.True(await frames.MoveNextAsync());
        if (before.Length > 0)
        {
            Expect.True((await stream.SendText(before, Token)).IsSuccess);
        }

        Expect.True((await stream.SendText(text, Token)).IsSuccess);
        while (!string.Join('\n', frames.Current.Rows).Contains(before + text, StringComparison.Ordinal))
        {
            Expect.True(await frames.MoveNextAsync());
        }
    }

    /// <summary>A session with no tmux pane is SND-AGENT-015.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_session_without_a_pane_is_015()
    {
        var source = await CreateRefusing(SindriErrors.AgentNoPane.Code);

        Expect.Equal("SND-AGENT-015", (await source.Open(new SessionRef(AgentId.From("claude-code"), SessionId.From("warp")), size: null, Token)).Error.Code);
    }

    /// <summary>A gone tmux server or session is SND-AGENT-016.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_gone_session_is_016()
    {
        var source = await CreateRefusing(SindriErrors.AgentPaneGone.Code);

        Expect.Equal("SND-AGENT-016", (await source.Open(new SessionRef(AgentId.From("claude-code"), SessionId.From("gone")), size: null, Token)).Error.Code);
    }

    private static async Task<IOpenTerminalPane> OpenLive(ITerminalPaneSource source) =>
        (await source.Open(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), size: null, Token)).Value;

    private static FrameReader Frames(IOpenTerminalPane stream) => new(stream);

    /// <summary>One frame enumerator over a stream, read forward with a 5 s limit per wait.</summary>
    private sealed class FrameReader(IOpenTerminalPane stream) : IAsyncDisposable
    {
        private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(Token);
        private IAsyncEnumerator<TerminalFrame>? _frames;

        public async Task<TerminalFrame> Next(Func<TerminalFrame, bool> wanted)
        {
            _frames ??= stream.Frames(_stop.Token).GetAsyncEnumerator(_stop.Token);
            _stop.CancelAfter(TimeSpan.FromSeconds(5));
            while (await _frames.MoveNextAsync())
            {
                if (wanted(_frames.Current))
                {
                    _stop.CancelAfter(Timeout.InfiniteTimeSpan);
                    return _frames.Current;
                }
            }

            throw new InvalidOperationException("the frames ended before one matched");
        }

        /// <summary>The frames that arrive in the next <paramref name="span"/>; the reader is spent after it.</summary>
        public async Task<List<TerminalFrame>> Window(TimeSpan span)
        {
            _frames ??= stream.Frames(_stop.Token).GetAsyncEnumerator(_stop.Token);
            var seen = new List<TerminalFrame>();
            _stop.CancelAfter(span);
            try
            {
                while (await _frames.MoveNextAsync())
                {
                    seen.Add(_frames.Current);
                }
            }
            catch (OperationCanceledException)
            {
                // the window ended
            }

            return seen;
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            if (_frames is not null)
            {
                await _frames.DisposeAsync();
            }

            _stop.Dispose();
        }
    }

    private static async Task<TerminalFrame> FirstFrame(IOpenTerminalPane stream)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(5));
        await using var frames = stream.Frames(limit.Token).GetAsyncEnumerator(limit.Token);
        Expect.True(await frames.MoveNextAsync());
        return frames.Current;
    }
}
