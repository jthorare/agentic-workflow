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

    /// <summary>#3262: a resource that throws while stopping must not skip the owned fixture dirs and the home.</summary>
    [Fact(Timeout = 60_000)]
    public async Task A_throwing_running_resource_still_disposes_what_the_host_owns()
    {
        var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        var probe = new HomeProbe(host.Home);
        host.Own(new ThrowingRunning());
        host.Own(probe);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => host.DisposeAsync().AsTask());

        Assert.True(probe.Disposed, "an owned resource was skipped after a running resource threw");
        Assert.False(Directory.Exists(host.Home), "the home is still there after a running resource threw");
    }

    /// <summary>#3262: one owned resource that throws must not skip the next one.</summary>
    [Fact(Timeout = 60_000)]
    public async Task A_throwing_owned_resource_does_not_skip_the_next_one()
    {
        var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        var probe = new HomeProbe(host.Home);
        host.Own(new ThrowingOwned());
        host.Own(probe);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => host.DisposeAsync().AsTask());

        Assert.True(probe.Disposed, "the owned resource after a throwing one was skipped");
        Assert.False(Directory.Exists(host.Home), "the home is still there");
    }

    private sealed class ThrowingRunning : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => throw new InvalidOperationException("stop failed");
    }

    private sealed class ThrowingOwned : IDisposable
    {
        public void Dispose() => throw new InvalidOperationException("dispose failed");
    }

    private sealed class HomeProbe(string home) : IDisposable
    {
        public bool HomeExistedAtDispose { get; private set; }

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
            HomeExistedAtDispose = Directory.Exists(home);
        }
    }
}
