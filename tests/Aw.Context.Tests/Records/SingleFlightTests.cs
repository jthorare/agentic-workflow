using Aw.Context.Infrastructure.Records;

namespace Aw.Context.Tests.Records;

/// <summary>
/// Concurrent status reads share one in-flight read instead of each starting its own (#1951: 98 at once), and the shared read
/// stops only when every caller has gone.
/// </summary>
[Trait("Lane", "unit")]
public sealed class SingleFlightTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Concurrent_callers_share_one_run_and_get_its_value()
    {
        var flight = new SingleFlight<int>();
        var work = new Work();

        var first = flight.Run(work.Start, Ct).AsTask();
        var second = flight.Run(work.Start, Ct).AsTask();
        await work.Started.Task.WaitAsync(Ct);
        work.Finish(7);

        var values = await Task.WhenAll(first, second);
        Assert.Equal([7, 7], values);
        Assert.Equal(1, work.Runs);
    }

    [Fact]
    public async Task A_call_after_the_run_finished_starts_a_new_run()
    {
        var flight = new SingleFlight<int>();
        var work = new Work();
        var first = flight.Run(work.Start, Ct).AsTask();
        await work.Started.Task.WaitAsync(Ct);
        work.Finish(1);
        _ = await first;

        var again = new Work();
        var second = flight.Run(again.Start, Ct).AsTask();
        await again.Started.Task.WaitAsync(Ct);
        again.Finish(2);

        Assert.Equal(2, await second);
        Assert.Equal(1, again.Runs);
    }

    [Fact]
    public async Task One_caller_leaving_does_not_stop_the_run_the_other_still_waits_on()
    {
        var flight = new SingleFlight<int>();
        var work = new Work();
        using var leaving = new CancellationTokenSource();
        var gone = flight.Run(work.Start, leaving.Token).AsTask();
        var staying = flight.Run(work.Start, Ct).AsTask();
        await work.Started.Task.WaitAsync(Ct);

        await leaving.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gone);
        Assert.False(work.Token.IsCancellationRequested);
        work.Finish(3);

        Assert.Equal(3, await staying);
    }

    [Fact]
    public async Task The_run_is_cancelled_when_every_caller_has_left_and_the_next_call_starts_afresh()
    {
        var flight = new SingleFlight<int>();
        var work = new Work();
        using var leaving = new CancellationTokenSource();
        var gone = flight.Run(work.Start, leaving.Token).AsTask();
        await work.Started.Task.WaitAsync(Ct);

        await leaving.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gone);
        Assert.True(work.Token.IsCancellationRequested);

        var fresh = new Work();
        var next = flight.Run(fresh.Start, Ct).AsTask();
        await fresh.Started.Task.WaitAsync(Ct);
        fresh.Finish(4);

        Assert.Equal(4, await next);
    }

    [Fact]
    public async Task A_failed_run_fails_its_callers_and_the_next_call_runs_again()
    {
        var flight = new SingleFlight<int>();
        var work = new Work();
        var failing = flight.Run(work.Start, Ct).AsTask();
        await work.Started.Task.WaitAsync(Ct);
        work.Fail(new InvalidOperationException("boom"));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => failing);

        var again = new Work();
        var next = flight.Run(again.Start, Ct).AsTask();
        await again.Started.Task.WaitAsync(Ct);
        again.Finish(5);
        Assert.Equal(5, await next);
    }

    [Fact]
    public async Task Runs_that_finish_at_once_never_fail_their_callers()
    {
        // why: a run that finishes before its landing is hooked up must not take the flight away from the caller that started it
        var flight = new SingleFlight<int>();

        var values = await Task.WhenAll(Enumerable.Range(0, 20000).Select(i => Task.Run(async () => await flight.Run(_ => Task.FromResult(i), Ct), Ct)));

        Assert.Equal(20000, values.Length);
    }

    /// <summary>Work the test finishes by hand; it counts its runs and keeps the token it was given.</summary>
    private sealed class Work
    {
        private readonly TaskCompletionSource<int> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Runs { get; private set; }

        public CancellationToken Token { get; private set; }

        public Task<int> Start(CancellationToken cancellationToken)
        {
            Runs++;
            Token = cancellationToken;
            _ = Started.TrySetResult();
            return _result.Task;
        }

        public void Finish(int value) => _result.SetResult(value);

        public void Fail(Exception exception) => _result.SetException(exception);
    }
}
