using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.TerminalSocket;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.TerminalSocket;

/// <summary>The terminal WebSocket's protocol (agents spec §15.8; A8 red), driven over a fake channel and the fixture-playing pane source.</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalSocketSessionTests
{
    private const string Token = "good-token";
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string Hello(string token = Token) => $$"""{"t":"hello","v":1,"token":"{{token}}","cols":120,"rows":36}""";

    private static (TerminalSocketSession Session, FakeTerminalPaneSource Source, FakeTime Time) Build(FakeTerminalPaneSource? source = null)
    {
        var time = new FakeTime();
        var fake = source ?? FakeTerminalPaneSource.FromFixture();
        return (new TerminalSocketSession(fake, t => t == Token, time.Provider), fake, time);
    }

    [Fact]
    public async Task A_good_hello_is_answered_by_ready_then_the_first_frame()
    {
        var (session, source, _) = Build();
        var channel = new FakeTerminalChannel(Hello());

        var run = session.Run(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), channel, Cancel).AsTask();
        await channel.WaitForSent(2, Cancel);
        channel.End();
        await run;

        using var ready = JsonDocument.Parse(channel.Sent[0]);
        Assert.Equal("ready", ready.RootElement.GetProperty("t").GetString());
        Assert.Equal(source.Host.Pane, ready.RootElement.GetProperty("host").GetProperty("pane").GetString());
        using var frame = JsonDocument.Parse(channel.Sent[1]);
        Assert.Equal("frame", frame.RootElement.GetProperty("t").GetString());
        Assert.Equal(1, frame.RootElement.GetProperty("seq").GetInt64());
    }

    [Fact]
    public async Task A_bad_token_closes_1008_with_SND_AUTH_002()
    {
        var (session, _, _) = Build();
        var channel = new FakeTerminalChannel(Hello("wrong"));

        await session.Run(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), channel, Cancel);

        Assert.Equal((1008, "SND-AUTH-002"), channel.Closed);
        Assert.Empty(channel.Sent);
    }

    [Fact]
    public async Task No_hello_in_3_seconds_closes_1008()
    {
        var (session, _, time) = Build();
        var channel = new FakeTerminalChannel();

        var run = session.Run(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), channel, Cancel).AsTask();
        time.Advance(TimeSpan.FromSeconds(3.1));
        await run;

        Assert.Equal(1008, channel.Closed?.Code);
    }

    [Theory]
    [InlineData("SND-AGENT-015", 4015)]
    [InlineData("SND-AGENT-016", 4016)]
    public async Task A_pane_that_cannot_open_closes_4015_or_4016(string code, int closeCode)
    {
        var source = FakeTerminalPaneSource.FromFixture();
        source.OpenFails = (code == "SND-AGENT-015" ? SindriErrors.AgentNoPane : SindriErrors.AgentPaneGone).With("fake");
        var (session, _, _) = Build(source);
        var channel = new FakeTerminalChannel(Hello());

        await session.Run(new SessionRef(AgentId.From("claude-code"), SessionId.From("warp")), channel, Cancel);

        Assert.Equal((closeCode, code), channel.Closed);
    }

    [Fact]
    public async Task A_key_message_reaches_the_pane_and_is_answered_by_a_frame_before_the_next_tick()
    {
        var (session, source, _) = Build();
        var channel = new FakeTerminalChannel(Hello(), """{"t":"key","key":"Enter"}""");

        var run = session.Run(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), channel, Cancel).AsTask();
        await channel.WaitForSent(3, Cancel);   // ready, first frame, the frame the key made — the fake clock never ticked
        channel.End();
        await run;

        Assert.Equal(["key:Enter"], source.Sent);
    }

    [Fact]
    public async Task A_key_outside_the_list_is_dropped_with_no_echo()
    {
        var (session, source, _) = Build();
        var channel = new FakeTerminalChannel(Hello(), """{"t":"key","key":"F13"}""");

        var run = session.Run(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), channel, Cancel).AsTask();
        await channel.WaitForSent(2, Cancel);
        channel.End();
        await run;

        Assert.Empty(source.Sent);
        Assert.Equal(2, channel.Sent.Count);
    }

    [Fact]
    public async Task A_paste_over_256_KiB_closes_1009()
    {
        var (session, _, _) = Build();
        var channel = new FakeTerminalChannel(Hello(), "{\"t\":\"paste\",\"text\":\"" + new string('a', (256 * 1024) + 1) + "\"}");

        await session.Run(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), channel, Cancel);

        Assert.Equal(1009, channel.Closed?.Code);
    }

    [Fact]
    public async Task Closing_the_socket_detaches_and_does_not_stop_the_session()
    {
        var (session, source, _) = Build();
        var channel = new FakeTerminalChannel(Hello());

        var run = session.Run(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), channel, Cancel).AsTask();
        await channel.WaitForSent(2, Cancel);
        channel.End();
        await run;

        Assert.Equal(1, source.Detached);
    }

    [Fact]
    public async Task No_pong_in_10_seconds_after_a_ping_closes_the_socket()
    {
        var (session, _, time) = Build();
        var channel = new FakeTerminalChannel(Hello());

        var run = session.Run(new SessionRef(AgentId.From("claude-code"), SessionId.From("live")), channel, Cancel).AsTask();
        await channel.WaitForSent(2, Cancel);
        time.Advance(TimeSpan.FromSeconds(15));
        await channel.WaitForSent(3, Cancel);   // the ping
        time.Advance(TimeSpan.FromSeconds(10.1));
        await run;

        _ = Assert.NotNull(channel.Closed);
        Assert.Contains("\"t\":\"ping\"", channel.Sent[^1], StringComparison.Ordinal);
    }
}
