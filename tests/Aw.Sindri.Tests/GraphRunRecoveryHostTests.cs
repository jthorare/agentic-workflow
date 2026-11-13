using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Host.Server;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests;

/// <summary>Issue 2999: after its first pass the recovery host keeps sweeping about every minute, over runs older than five minutes.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphRunRecoveryHostTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class RecordingRecovery(FakeTimeProvider clock) : IGraphRunRecovery
    {
        private readonly ConcurrentQueue<(DateTimeOffset Cutoff, DateTimeOffset Now)> _passes = new();

        public IReadOnlyList<(DateTimeOffset Cutoff, DateTimeOffset Now)> Passes => [.. _passes];

        public ValueTask<Result<int>> Run(CancellationToken cancellationToken) => Run(DateTimeOffset.MaxValue, cancellationToken);

        public ValueTask<Result<int>> Run(DateTimeOffset startedBefore, CancellationToken cancellationToken)
        {
            _passes.Enqueue((startedBefore, clock.GetUtcNow()));
            return ValueTask.FromResult<Result<int>>(0);
        }
    }

    [Fact]
    public async Task The_host_repeats_its_pass_every_minute_with_a_five_minute_margin()
    {
        var harness = new SliceHarness();
        _ = await harness.HoldLease();
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var recovery = new RecordingRecovery(clock);
        using var host = new GraphRunRecoveryHost(recovery, harness.Held, clock, NullLogger<GraphRunRecoveryHost>.Instance);

        await host.StartAsync(Token);
        // why: the host's timers are on the fake clock; advance in steps until it has swept three times, with a real pause so it can register each timer
        for (var tries = 0; tries < 400 && recovery.Passes.Count < 3; tries++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, Token);
            clock.Advance(TimeSpan.FromMinutes(1));
        }

        await host.StopAsync(Token);
        var passes = recovery.Passes;
        Assert.True(passes.Count >= 3, $"the host made {passes.Count} pass(es), not at least 3");   // red today: one pass, then the host returns
        Assert.Equal(KnownInstants.Epoch, passes[0].Cutoff);
        Assert.All(passes.Skip(1), static p => Assert.Equal(p.Now - TimeSpan.FromMinutes(5), p.Cutoff));
    }
}
