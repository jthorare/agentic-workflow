using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Extract;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.BuildMap;

/// <summary>#2106: a caller waiting for the map build slot gives up with SND-CONTEXT-260 only when the holder has written no line for 30 min, keeps waiting while the holder writes through its turn's progress, says every 5 min that it still waits, takes the slot when its holder leaves, and still throws when its own token is cancelled.</summary>
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
    public async Task A_waiter_behind_a_holder_that_writes_every_10_minutes_waits_for_60_minutes_and_gets_the_gate()
    {
        using var gate = new MapBuildGate(_clock);
        var holder = (await gate.Enter("JOB-A", progress: null, Ct)).Value;
        var second = gate.Enter("JOB-B", progress: null, Ct).AsTask();

        for (var minute = 1; minute <= 60; minute++)
        {
            if (minute % 10 == 0)
            {
                holder.Progress.Report("extracting " + minute.ToString(CultureInfo.InvariantCulture) + "/60 files");
            }

            _clock.Advance(TimeSpan.FromMinutes(1));
            await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, Ct);
            Assert.False(second.IsCompleted, "the waiter gave up at minute " + minute.ToString(CultureInfo.InvariantCulture) + " although the holder wrote a line in the last 10 min");
        }

        holder.Dispose();
        var result = await second.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, Ct);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Code : null);
        Assert.True(result.Value.Waited);
        result.Value.Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_waiter_gives_up_30_minutes_after_the_holders_last_line_not_30_minutes_after_it_started_waiting()
    {
        using var gate = new MapBuildGate(_clock);
        using var holder = (await gate.Enter("JOB-A", progress: null, Ct)).Value;
        var second = gate.Enter("JOB-B", progress: null, Ct).AsTask();
        await Drive(second, 20);
        holder.Progress.Report("graphify running, 20 min");

        await Drive(second, 25);   // 45 min in: 25 min after the holder's last line
        Assert.False(second.IsCompleted, "the waiter gave up 25 min after the holder's last line");
        await Drive(second, 10);   // 55 min in: 35 min after it

        Assert.True(second.IsCompleted, "the waiter is still waiting 35 min after the holder's last line");
        var result = await second;
        Assert.Equal(ContextErrors.MapBuildWaitTimedOut.Code, result.Error.Code);
        Assert.Contains("JOB-A", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("wrote no line for 30 min", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_waiter_says_every_5_minutes_that_it_still_waits_and_how_long_ago_the_holder_wrote()
    {
        using var gate = new MapBuildGate(_clock);
        using var holder = (await gate.Enter("JOB-A", progress: null, Ct)).Value;
        var progress = new SyncProgress();
        var second = gate.Enter("JOB-B", progress, Ct).AsTask();

        await Drive(second, 12);
        holder.Progress.Report("graphify running, 12 min");
        await Drive(second, 8);

        string[] expected =
        [
            "waiting for the map build of job JOB-A",
            "still waiting for the map build of job JOB-A (its last line 5 min ago)",
            "still waiting for the map build of job JOB-A (its last line 10 min ago)",
            "still waiting for the map build of job JOB-A (its last line 3 min ago)",
            "still waiting for the map build of job JOB-A (its last line 8 min ago)",
        ];
        Assert.True(expected.SequenceEqual(progress.Lines, StringComparer.Ordinal), string.Join(" | ", progress.Lines));
        holder.Dispose();
        (await second).Value.Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_holders_lines_reach_the_progress_it_entered_with_and_a_null_progress_is_fine()
    {
        using var gate = new MapBuildGate(_clock);
        var progress = new SyncProgress();
        using (var told = (await gate.Enter("JOB-A", progress, Ct)).Value)
        {
            told.Progress.Report("extracting 1/2 files");
        }

        using var silent = (await gate.Enter("JOB-B", progress: null, Ct)).Value;
        silent.Progress.Report("extracting 2/2 files");

        Assert.Equal(["extracting 1/2 files"], progress.Lines);
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
