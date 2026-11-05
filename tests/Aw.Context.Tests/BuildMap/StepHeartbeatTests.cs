using Aw.Context.Core.Extract;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.BuildMap;

/// <summary>#2106: a silent step that bounds itself says it is alive every 5 min while it runs, and says nothing once it ends.</summary>
public sealed class StepHeartbeatTests
{
    private readonly FakeTimeProvider _clock = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Moves the fake clock in 1-min steps, up to the given minutes or until the task completes.
    private async Task Drive(Task task, int minutes)
    {
        for (var i = 0; i < minutes && !task.IsCompleted; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, Ct);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_45_minute_step_reports_every_5_minutes_while_it_runs_and_nothing_after_it_ends()
    {
        var progress = new SyncProgress();
        var run = StepHeartbeat.Run("graphify", progress, _clock, async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(45), _clock, Ct);
            return 7;
        }, Ct).AsTask();

        await Drive(run, 50);

        Assert.True(run.IsCompleted, "the step is still running after 50 min");
        Assert.Equal(7, await run);
        // why: the 45-min beat and the end of the step fall on the same tick, so only the beats before it are asserted
        string[] first = ["graphify running, 5 min", "graphify running, 10 min", "graphify running, 15 min", "graphify running, 20 min", "graphify running, 25 min", "graphify running, 30 min", "graphify running, 35 min", "graphify running, 40 min"];
        Assert.True(first.SequenceEqual(progress.Lines.Take(8), StringComparer.Ordinal), string.Join(" | ", progress.Lines));
        Assert.True(progress.Lines.Count <= 9, string.Join(" | ", progress.Lines));
        var told = progress.Lines.Count;
        _clock.Advance(TimeSpan.FromMinutes(30));
        await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, Ct);
        Assert.Equal(told, progress.Lines.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_shorter_than_5_minutes_reports_nothing()
    {
        var progress = new SyncProgress();
        var run = StepHeartbeat.Run("graphify", progress, _clock, async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(3), _clock, Ct);
            return 1;
        }, Ct).AsTask();

        await Drive(run, 10);

        Assert.Equal(1, await run);
        Assert.Empty(progress.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_without_a_progress_just_runs()
    {
        var result = await StepHeartbeat.Run("graphify", progress: null, _clock, () => ValueTask.FromResult(3), Ct);

        Assert.Equal(3, result);
    }
}
