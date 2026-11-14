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

    private static async Task<TerminalFrame> FirstFrame(IOpenTerminalPane stream)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(5));
        await using var frames = stream.Frames(limit.Token).GetAsyncEnumerator(limit.Token);
        Expect.True(await frames.MoveNextAsync());
        return frames.Current;
    }
}
