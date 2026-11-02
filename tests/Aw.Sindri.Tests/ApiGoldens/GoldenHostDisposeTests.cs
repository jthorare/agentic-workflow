using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// #1414: what a host owns (the jobs tests' views pump) writes into its home, so it must stop before the home is deleted;
/// a pump still writing while the recursive delete runs fails it with "Directory not empty".
/// </summary>
[Lane(TestLane.Integration)]
public sealed class GoldenHostDisposeTests
{
    [Fact(Timeout = 60_000)]
    public async Task Owned_resources_stop_while_the_home_still_exists()
    {
        var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        var probe = new HomeProbe(host.Home);
        host.Own(probe);

        await host.DisposeAsync();

        Assert.True(probe.HomeExistedAtDispose, "the home was deleted before an owned resource was stopped");
        Assert.False(Directory.Exists(host.Home), "the home is still there after dispose");
    }

    private sealed class HomeProbe(string home) : IDisposable
    {
        public bool HomeExistedAtDispose { get; private set; }

        public void Dispose() => HomeExistedAtDispose = Directory.Exists(home);
    }
}
