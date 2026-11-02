using Aw.Kernel.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Infrastructure.Notifications;

namespace Aw.Sindri.Tests.Setup;

/// <summary>Doctor's 'setup:banners' line (GitHub #1111): on a Mac, terminal-notifier decides whether any desktop banner is shown.</summary>
[Lane(TestLane.Unit)]
public sealed class BannerDoctorChecksTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_Mac_without_terminal_notifier_warns_that_banners_are_off_and_names_the_fix()
    {
        var checks = await new BannerDoctorChecks(isMacOs: true, static () => null).Run(Token);

        Assert.Equal(new DoctorCheck("setup:banners", CheckState.Warn, "desktop banners are off: terminal-notifier isn't installed", "brew install terminal-notifier (or rerun ./setup.sh)"), Assert.Single(checks));
    }

    [Fact]
    public async Task A_Mac_with_terminal_notifier_is_ok()
    {
        var check = Assert.Single(await new BannerDoctorChecks(isMacOs: true, static () => "/opt/homebrew/bin/terminal-notifier").Run(Token));

        Assert.Equal((CheckState.Ok, null), (check.State, check.Fix));
    }

    [Fact]
    public async Task Other_systems_get_no_line() =>
        Assert.Empty(await new BannerDoctorChecks(isMacOs: false, static () => null).Run(Token));
}
