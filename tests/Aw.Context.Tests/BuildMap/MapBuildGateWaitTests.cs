using Aw.Context.Core;
using Aw.Context.Core.Extract;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.BuildMap;

/// <summary>#2106: a caller waiting for the map build slot gives up after 30 min with SND-CONTEXT-260, takes the slot when its holder leaves sooner, and still throws when its own token is cancelled.</summary>
public sealed class MapBuildGateWaitTests
{
    private readonly FakeTimeProvider _clock = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Moves the fake clock in 1-min steps, up to the given minutes or until the task completes.
    private async Task Drive(Task task, int minutes)
    {
        for (var i = 0; i < minutes && !task.IsCompleted; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            await Task.Delay(TimeSpan.FromMilliseconds(1), TimeProvider.System, Ct);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_enter_behind_a_held_gate_gives_up_after_30_minutes()
    {
        using var gate = new MapBuildGate(_clock);
        using var holder = (await gate.Enter("JOB-A", progress: null, Ct)).Value;

        var second = gate.Enter("JOB-B", progress: null, Ct).AsTask();
        await Drive(second, 31);

        Assert.True(second.IsCompleted, "the wait is still going 31 min in");
        var result = await second;
        Assert.Equal(ContextErrors.MapBuildWaitTimedOut.Code, result.Error.Code);
        Assert.Contains("30 min", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("JOB-A", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_enter_is_still_waiting_before_30_minutes()
    {
        using var gate = new MapBuildGate(_clock);
        using var holder = (await gate.Enter("JOB-A", progress: null, Ct)).Value;

        var second = gate.Enter("JOB-B", progress: null, Ct).AsTask();
        await Drive(second, 29);

        Assert.False(second.IsCompleted);
        holder.Dispose();
        (await second).Value.Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_waiter_gets_the_gate_when_its_holder_leaves_at_29_minutes()
    {
        using var gate = new MapBuildGate(_clock);
        var holder = (await gate.Enter("JOB-A", progress: null, Ct)).Value;
        var second = gate.Enter("JOB-B", progress: null, Ct).AsTask();
        await Drive(second, 29);

        holder.Dispose();
        _clock.Advance(TimeSpan.FromMinutes(5));

        var result = await second;
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Code : null);
        Assert.True(result.Value.Waited);
        result.Value.Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancelled_caller_token_still_throws()
    {
        using var gate = new MapBuildGate(_clock);
        using var holder = (await gate.Enter("JOB-A", progress: null, Ct)).Value;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);

        var second = gate.Enter("JOB-B", progress: null, cancel.Token).AsTask();
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_free_gate_is_taken_at_once()
    {
        using var gate = new MapBuildGate(_clock);

        var result = await gate.Enter("JOB-A", progress: null, Ct);

        Assert.False(result.Value.Waited);
        result.Value.Dispose();
    }
}
