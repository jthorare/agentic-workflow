using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

public sealed partial class FreshnessTests
{
    // #1972: the refresh goes on after the services read (the followed views), and the throttle's timer starts only when
    // it ends. A test that moves the clock before that loses the move.
    [Fact]
    public async Task A_burst_still_re_reads_once_per_window_when_the_refresh_ends_late()
    {
        _vm.Follow(async (_, _) => await Task.Delay(300, CancellationToken.None));
        await Connected(() => _server.ServicesCalls > 0);
        var (status, services) = (_server.StatusCalls, _server.ServicesCalls);

        _server.Push(A.Event(1, "service.ran"));
        await RefreshHandled(services + 1);
        _server.Push(A.Event(2, "service.ran"), A.Event(3, "service.ran"), A.Event(4, "service.ran"));
        await Settle();

        Assert.Equal((status + 1, services + 1), (_server.StatusCalls, _server.ServicesCalls));

        await ReReadAfterWindow(services + 2);
        await Settle();

        Assert.Equal((status + 2, services + 2), (_server.StatusCalls, _server.ServicesCalls));
    }
}
