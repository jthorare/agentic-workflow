using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.AgoTicks;

/// <summary>#3347: the sandbox card's "Checked 5m ago" moves with the clock on the one-second beat.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxCheckedAgoTickTests
{
    [Fact]
    public Task The_checked_words_move_with_the_clock() => UiThread.Run(async () =>
    {
        var host = new AgoTickHost(AgoTickHost.GoldenDay + TimeSpan.FromMinutes(5));   // the golden's checkedAt is the golden day itself
        host.Server.SandboxStatusAnswer = RouteAnswer.SpecShaped("sandbox-status", "after-ready-check");
        var shell = await host.Start();
        var check = shell.Health.Sandbox.Check;
        await AgoTickHost.Until(() => check.CheckedText.Length > 0);
        Assert.Equal("Checked 5m ago", check.CheckedText);

        host.Clock.Advance(TimeSpan.FromMinutes(2));

        await AgoTickHost.Until(() => string.Equals(check.CheckedText, "Checked 7m ago", StringComparison.Ordinal));
    }, AgoTickHost.Token);
}
