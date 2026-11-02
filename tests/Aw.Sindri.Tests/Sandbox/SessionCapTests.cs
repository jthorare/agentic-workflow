using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>The atomic session cap (sandbox spec §3.3 "Concurrent sessions", §3.9).</summary>
[Lane(TestLane.Unit)]
public sealed class SessionCapTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-cap-").FullName;
    private readonly FakeSliceScopes _scopes = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Start_runs_when_live_is_below_the_cap()
    {
        var cap = Cap();
        _scopes.Add("sindri-a.scope");

        var result = await cap.Start(2, _ => Task.FromResult<Result<int>>(7), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task Start_refuses_with_020_and_does_not_start_when_live_reaches_the_cap()
    {
        var cap = Cap();
        _scopes.Add("sindri-a.scope");
        var started = false;

        var result = await cap.Start(1, _ =>
        {
            started = true;
            return Task.FromResult<Result<int>>(1);
        }, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.LaunchSessionWait.Code, result.Error.Code);
        Assert.False(started);
    }

    [Fact]
    public async Task Lock_is_released_when_start_throws()
    {
        var cap = Cap();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cap.Start<int>(1, _ => throw new InvalidOperationException("boom"), TestContext.Current.CancellationToken));
        var again = await cap.Start(1, _ => Task.FromResult<Result<int>>(2), TestContext.Current.CancellationToken);

        Assert.Equal(2, again.Value);
    }

    [Fact]
    public async Task Cap_holds_when_two_launches_race()
    {
        var ct = TestContext.Current.CancellationToken;
        var one = Cap();
        var two = Cap(); // a second instance: only the lock file is shared, as between two serve threads

        async Task<Result<int>> Launch(FileSessionCap c, string unit) => await c.Start(1, async Task<Result<int>> (inner) =>
        {
            await Task.Delay(150, inner);
            _scopes.Add(unit);
            return 1;
        }, ct);

        var results = await Task.WhenAll(Launch(one, "sindri-1.scope"), Launch(two, "sindri-2.scope"));

        Assert.Equal(1, results.Count(r => r.IsSuccess));
        var refused = Assert.Single(results, r => r.IsError);
        Assert.Equal(SindriErrors.LaunchSessionWait.Code, refused.Error.Code);
        _ = Assert.Single(await _scopes.Live(ct));
    }

    private FileSessionCap Cap() => new(Path.Combine(_dir, "locks", "sessions.lock"), _scopes);
}
