using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

[Lane(TestLane.Unit)]
public sealed class StopConfirmTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stop_Sindri_asks_first_and_sends_nothing_until_confirmed()
    {
        var system = new SystemTestHarness();
        await system.Show();

        await system.Vm.Catalog.Stop!.Press(Token);

        Assert.Equal((true, 0), (system.Vm.Catalog.Stop.IsConfirming, system.Server.StopCalls));
    }

    [Fact]
    public async Task Keep_running_sends_nothing()
    {
        var system = new SystemTestHarness();
        await system.Show();
        await system.Vm.Catalog.Stop!.Press(Token);

        system.Vm.Catalog.Stop.Keep();

        Assert.Equal((false, 0), (system.Vm.Catalog.Stop.IsConfirming, system.Server.StopCalls));
    }

    [Fact]
    public async Task Confirm_sends_the_stop_once()
    {
        var system = new SystemTestHarness();
        await system.Show();
        await system.Vm.Catalog.Stop!.Press(Token);

        await system.Vm.Catalog.Stop.Confirm(Token);

        Assert.Equal((1, true), (system.Server.StopCalls, system.Vm.Catalog.Stop.IsAccepted));
    }

    [Fact]
    public async Task A_refused_stop_shows_the_servers_code()
    {
        var system = new SystemTestHarness();
        system.Server.StopAnswer = RouteAnswer.Problem("stop", "refused-session");
        await system.Show();
        await system.Vm.Catalog.Stop!.Press(Token);

        await system.Vm.Catalog.Stop.Confirm(Token);

        Assert.Equal(("SND-AUTH-002", true), (system.Vm.Catalog.Stop.ErrorCode, system.Vm.Catalog.Stop.IsFailed));
    }
}
