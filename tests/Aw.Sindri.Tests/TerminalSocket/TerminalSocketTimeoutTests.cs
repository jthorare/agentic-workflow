using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.TerminalSocket;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.TerminalSocket;

/// <summary>A pane call that hits its call budget surfaces as a cancel nobody asked for; the socket answers with a close code, never a silent stop (agents spec §15.8).</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalSocketTimeoutTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;
    private static readonly TimeSpan _within = TimeSpan.FromSeconds(5);

    private static string Hello() => """{"t":"hello","v":1,"token":"good-token","cols":120,"rows":36}""";

    private static TerminalSocketSession Build(FakeTerminalPaneSource source) => new(source, t => t == "good-token", new FakeTime().Provider);

    private static SessionRef Live() => new(AgentId.From("claude-code"), SessionId.From("live"));

    [Fact]
    public async Task A_capture_that_times_out_closes_4016_instead_of_freezing_the_screen()
    {
        var source = FakeTerminalPaneSource.FromFixture();
        source.CaptureTimesOut = true;
        var channel = new FakeTerminalChannel(Hello());

        await Build(source).Run(Live(), channel, Cancel).AsTask().WaitAsync(_within, Cancel);

        Assert.Equal(4016, channel.Closed?.Code);
    }

    [Fact]
    public async Task A_send_that_times_out_closes_4016_instead_of_ending_without_a_close_frame()
    {
        var source = FakeTerminalPaneSource.FromFixture();
        source.SendTimesOut = true;
        var channel = new FakeTerminalChannel(Hello(), """{"t":"key","key":"Enter"}""");

        await Build(source).Run(Live(), channel, Cancel).AsTask().WaitAsync(_within, Cancel);

        Assert.Equal(4016, channel.Closed?.Code);
    }

    [Fact]
    public async Task A_typed_text_send_that_times_out_closes_4016()
    {
        var source = FakeTerminalPaneSource.FromFixture();
        source.SendTimesOut = true;
        var channel = new FakeTerminalChannel(Hello(), """{"t":"key","text":"ls"}""");

        await Build(source).Run(Live(), channel, Cancel).AsTask().WaitAsync(_within, Cancel);

        Assert.Equal(4016, channel.Closed?.Code);
    }
}
