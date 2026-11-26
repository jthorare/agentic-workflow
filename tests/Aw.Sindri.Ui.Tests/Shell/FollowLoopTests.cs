using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The Shell tests' teardown holds on a loaded box (#4439): when every thread-pool thread is busy, a cancelled follow ends only once the
/// pool frees a thread, so a teardown bound to 2 s of real time failed tests that had passed. This class holds the pool, so it runs alone.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(BusyPool.Name)]
public sealed class FollowLoopTests
{
    private static readonly TimeSpan _held = TimeSpan.FromMilliseconds(2500);

    [Fact]
    public async Task The_teardown_waits_out_a_busy_thread_pool()
    {
        var records = new InMemoryEndpointRecords();
        var server = new FakeSindriApi { Current = A.Status() with { Health = new HealthView("healthy", []) } };
        records.Write(A.Record());
        using var ui = new InlineUiContext();
        var vm = new HealthViewModel(records, server, new FakeTimeProvider(A.Noon), new FakeClipboard());
        var loop = new FollowLoop();
        loop.Start(vm.Run);
        for (var i = 0; i < 2000 && !vm.IsRunning; i++)
        {
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }

        Assert.True(vm.IsRunning, "running");
        // Never disposed: pool items queued behind the held ones still wait on it after the test ends.
#pragma warning disable CA2000
        var release = new ManualResetEventSlim();
#pragma warning restore CA2000
        HoldThePool(release);
        var releaser = new Thread(() =>
        {
            _ = release.WaitHandle.WaitOne(_held);
            release.Set();
        });
        releaser.Start();

        try
        {
            await loop.DisposeAsync();
        }
        finally
        {
            release.Set();
            releaser.Join();
        }
    }

    // Queues more blocking items than the pool has threads, so the follow's continuations wait until `release` is set; then lets them start.
    private static void HoldThePool(ManualResetEventSlim release)
    {
        ThreadPool.GetMinThreads(out var workers, out _);
        for (var i = 0; i < workers + 4; i++)
        {
            _ = ThreadPool.UnsafeQueueUserWorkItem(_ => release.Wait(TestContext.Current.CancellationToken), state: null);
        }

        RealTime.Sleep(TimeSpan.FromMilliseconds(200));
    }
}
