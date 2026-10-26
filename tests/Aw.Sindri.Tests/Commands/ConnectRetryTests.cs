using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Commands;

/// <summary>A launched session's report waits out a control plane that is down (issue #1000): bounded, doubling pauses, only on 'not running'.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectRetryTests
{
    private readonly StepClock _clock = new();
    private readonly List<string> _notices = [];
    private int _attempts;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Task Notice(string line)
    {
        _notices.Add(line);
        return Task.CompletedTask;
    }

    private Task<Result<int>> Run(Func<int, Result<int>> answer) =>
        _clock.Drive(ConnectRetry.Run(() => Task.FromResult(answer(++_attempts)), _clock, Notice, Token));

    [Fact]
    public async Task A_control_plane_that_never_comes_back_is_asked_for_two_minutes_and_the_last_not_running_comes_back()
    {
        var started = _clock.GetUtcNow();

        var result = await Run(_ => SindriErrors.NotRunning.With("no control plane is recorded"));

        Assert.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
        Assert.Equal(ConnectRetry.Window, _clock.GetUtcNow() - started);
        Assert.Equal(12, _attempts);   // at 0, 1, 3, 7, 15, 30, 45, 60, 75, 90, 105 and 120 s
    }

    [Fact]
    public async Task The_pauses_double_from_one_second_up_to_fifteen_and_each_wait_prints_one_line()
    {
        _ = await Run(_ => SindriErrors.NotRunning.With("down"));

        Assert.Equal(11, _notices.Count);
        Assert.Equal([1, 2, 4, 8, 15, 15, 15, 15, 15, 15, 15], _notices.Select(static n => int.Parse(n.Split("retrying in ")[1].Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture)));
        Assert.All(_notices, static n => Assert.StartsWith("SND-API-001 ", n, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_control_plane_that_comes_back_ends_the_wait_with_its_answer()
    {
        var result = await Run(attempt => attempt < 3 ? SindriErrors.NotRunning.With("restarting") : 7);

        Assert.Equal(7, result.Value);
        Assert.Equal(3, _attempts);
        Assert.Equal(2, _notices.Count);
    }

    [Fact]
    public async Task A_refusal_comes_back_at_once_with_no_wait()
    {
        var result = await Run(_ => WorkflowErrors.StaleRunToken.With("run 1.1 was already reported"));

        Assert.Equal(WorkflowErrors.StaleRunToken.Code, result.Error.Code);
        Assert.Equal(1, _attempts);
        Assert.Empty(_notices);
    }

    [Fact]
    public async Task A_timeout_comes_back_at_once_because_the_control_plane_is_up_and_slow()
    {
        var result = await Run(_ => SindriErrors.ApiTimeout.With("didn't answer within 30 s"));

        Assert.Equal(SindriErrors.ApiTimeout.Code, result.Error.Code);
        Assert.Equal(1, _attempts);
    }
}
