using Aw.Kernel.Testing;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Testing.Engine;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>The stuck-instance count GET /status folds into health (API spec §3.8, E-30, P13): read from the engine, kept for ten seconds, and never an error (status is the page that says the engine is down).</summary>
[Lane(TestLane.Unit)]
public sealed class StuckInstanceCountTests
{
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeEngineReader _engine = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private void Stuck(string address) => _engine.Running.Add(EngineFacts.Instance(address, EngineFacts.Wait(EngineWaitKinds.Signal, KnownInstants.Epoch.AddMinutes(-6))));

    [Fact]
    public async Task It_counts_the_instances_that_are_stuck()
    {
        Stuck("T-1/step/1");
        Stuck("T-2/step/1");
        _engine.Running.Add(EngineFacts.Instance("T-3/step/1", EngineFacts.Wait(EngineWaitKinds.Activity, KnownInstants.Epoch.AddMinutes(-6), "step.checks")));

        Assert.Equal(2, await new StuckInstanceCount(_engine, _clock).Current(Token));
    }

    [Fact]
    public async Task A_count_is_kept_for_ten_seconds_then_read_again()
    {
        var count = new StuckInstanceCount(_engine, _clock);
        Stuck("T-1/step/1");
        Assert.Equal(1, await count.Current(Token));

        Stuck("T-2/step/1");
        _clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal(1, await count.Current(Token));
        Assert.Equal(1, _engine.Calls["Instances"]);

        _clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(2, await count.Current(Token));
    }

    [Fact]
    public async Task A_server_that_does_not_answer_counts_none_instead_of_failing_the_status()
    {
        Stuck("T-1/step/1");
        _engine.Down = true;

        Assert.Equal(0, await new StuckInstanceCount(_engine, _clock).Current(Token));
    }

    [Fact(Timeout = 20_000)]
    public async Task A_read_that_never_answers_counts_none_after_two_seconds()
    {
        _engine.Hold = new TaskCompletionSource();

        Assert.Equal(0, await new StuckInstanceCount(_engine, _clock).Current(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_read_that_throws_something_other_than_an_error_result_counts_none()
    {
        _engine.Throws = new InvalidOperationException("closed");

        Assert.Equal(0, await new StuckInstanceCount(_engine, _clock).Current(Token));
    }

    [Fact]
    public async Task Concurrent_misses_share_one_read()
    {
        Stuck("T-1/step/1");
        _engine.Hold = new TaskCompletionSource();
        var count = new StuckInstanceCount(_engine, _clock);

        var first = count.Current(Token).AsTask();
        var second = count.Current(Token).AsTask();
        while (!_engine.Calls.ContainsKey("Instances"))
        {
            await Task.Delay(1, Token);
        }

        _engine.Hold.SetResult();

        var counts = await Task.WhenAll(first, second);

        Assert.Equal([1, 1], counts);
        Assert.Equal(1, _engine.Calls["Instances"]);
    }
}
