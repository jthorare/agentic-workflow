using Aw.Context.Core;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>Callers that miss the stamped cache together share one load; a load that fails or throws is not kept, so the next caller loads again.</summary>
public sealed class StampedCacheSingleFlightTests
{
    private const int Callers = 8;

    [Fact, Trait("Lane", "unit")]
    public async Task Concurrent_callers_on_a_cold_cache_run_one_load()
    {
        var cache = new StampedCache<string>();
        var loads = 0;
        var release = new TaskCompletionSource<Result<string?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Result<string?>> Load()
        {
            _ = Interlocked.Increment(ref loads);
            return release.Task;
        }

        var calls = Enumerable.Range(0, Callers).Select(_ => Task.Run(() => cache.GetOrLoad("current", 7, Load, TestContext.Current.CancellationToken).AsTask())).ToList();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        release.SetResult(Results.Success<string?>("map"));
        var results = await Task.WhenAll(calls);

        Assert.Equal(1, loads);
        Assert.All(results, r => Assert.Equal("map", r.Value));
        Assert.Equal("map", cache.Get("current", 7));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_load_is_not_kept_and_the_next_caller_loads_again()
    {
        var cache = new StampedCache<string>();
        var loads = 0;
        Task<Result<string?>> Load()
        {
            loads++;
            return Task.FromResult(loads == 1 ? Results.Failure<string?>(ContextErrors.NoMap.With("none")) : Results.Success<string?>("map"));
        }

        var first = await cache.GetOrLoad("current", 1, Load, TestContext.Current.CancellationToken);
        var second = await cache.GetOrLoad("current", 1, Load, TestContext.Current.CancellationToken);

        Assert.True(first.IsError);
        Assert.Equal("map", second.Value);
        Assert.Equal(2, loads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_load_that_throws_is_not_kept_either()
    {
        var cache = new StampedCache<string>();
        var loads = 0;
        Task<Result<string?>> Load()
        {
            loads++;
            return loads == 1 ? throw new InvalidOperationException("disk") : Task.FromResult(Results.Success<string?>("map"));
        }

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await cache.GetOrLoad("current", 1, Load, TestContext.Current.CancellationToken));
        var again = await cache.GetOrLoad("current", 1, Load, TestContext.Current.CancellationToken);

        Assert.Equal("map", again.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_stamp_every_caller_loads()
    {
        var cache = new StampedCache<string>();
        var loads = 0;
        Task<Result<string?>> Load()
        {
            loads++;
            return Task.FromResult(Results.Success<string?>("map"));
        }

        _ = await cache.GetOrLoad("current", stamp: null, Load, TestContext.Current.CancellationToken);
        _ = await cache.GetOrLoad("current", stamp: null, Load, TestContext.Current.CancellationToken);

        Assert.Equal(2, loads);
    }
}
