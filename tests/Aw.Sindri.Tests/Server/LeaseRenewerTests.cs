using Aw.Sindri.Core;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Server;

/// <summary>The renewer on the fake clock: each step advances one renewal interval and waits for that renewal.</summary>
public sealed class LeaseRenewerTests : IDisposable
{
    private readonly SliceHarness _h = new();
    private readonly FakeShutdown _shutdown = new();
    private readonly FakeExitCode _exit = new();
    private readonly ListLogger<LeaseRenewer> _log = new();
    private readonly ScriptedLease _lease;
    private readonly SteppedClock _clock;
    private LeaseRenewer? _running;

    public LeaseRenewerTests()
    {
        _lease = new ScriptedLease(_h.Store);
        _clock = new SteppedClock(_h.Clock);
    }

    public void Dispose()
    {
        _running?.Dispose();
        _shutdown.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_renewal_extends_the_held_grant()
    {
        var grant = await _h.HoldLease();

        await Run(steps: 1);

        Assert.Equal(_h.Clock.GetUtcNow() + LeaseTiming.Ttl, _h.Held.Current!.ExpiresAt);   // why: a successful renewal holds the extended grant
        Assert.Equal(grant.Epoch, _h.Held.Current.Epoch);
        Assert.False(_shutdown.Requested);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lost_lease_clears_the_grant_and_stops_the_process_with_exit_1()
    {
        var grant = await _h.HoldLease();
        _ = await _h.Store.Release(grant, Token);   // the row no longer names this holder: renewing is SND-LEASE-002

        await Run(steps: 1);

        Assert.True(_shutdown.Requested);
        Assert.Null(_h.Held.Current);
        Assert.Equal(1, _exit.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Another_renewal_error_is_retried_until_the_grant_expires()
    {
        var grant = await _h.HoldLease();
        _lease.RenewError = SindriErrors.LedgerUnavailable.With("database is locked");

        await Run(steps: 2);   // 20 s and 40 s: the 60 s grant still stands
        var heldWhileRetrying = _h.Held.Current;
        var stoppedEarly = _shutdown.Requested;
        await Step();          // 60 s: expired

        Assert.Equal(grant, heldWhileRetrying);
        Assert.False(stoppedEarly);
        Assert.True(_shutdown.Requested);
        Assert.Null(_h.Held.Current);
        Assert.Equal(1, _exit.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_is_renewed_once_a_stop_is_requested()
    {
        _ = await _h.HoldLease();
        using var renewer = Renewer();
        await renewer.StartAsync(Token);

        await _clock.Armed();
        _shutdown.Request();
        _h.Clock.Advance(LeaseTiming.RenewEvery * 2);
        await renewer.StopAsync(Token);

        Assert.Equal(0, _lease.Renewals);
        Assert.Equal(0, _exit.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stop_during_a_renewal_keeps_the_grant_cleared_and_exits_0()
    {
        _ = await _h.HoldLease();
        _lease.BeforeRenew = _ =>
        {
            _h.Held.Clear();          // the stop, while the renewal is in flight: it succeeds anyway
            _shutdown.Request();
            return Task.CompletedTask;
        };

        await Run(steps: 1);

        Assert.Null(_h.Held.Current);   // why: the renewed grant isn't held again after the stop cleared it
        Assert.Equal(0, _exit.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_renewal_right_after_a_stop_released_the_lease_exits_0_and_logs_no_loss()
    {
        _ = await _h.HoldLease();
        _lease.BeforeRenew = async grant =>
        {
            _h.Held.Clear();          // the stop cleared and released the grant first: renewing it now is SND-LEASE-002
            _ = await _h.Store.Release(grant, Token);
        };

        await Run(steps: 1);

        Assert.Equal(0, _exit.Code);
        Assert.DoesNotContain(_log.Lines, l => l.Contains(SindriErrors.LeaseLost.Code, StringComparison.Ordinal));
        Assert.Null(_h.Held.Current);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stop_that_cancels_a_renewal_in_flight_ends_the_renewer_cleanly()
    {
        _ = await _h.HoldLease();
        _lease.BeforeRenew = _ =>
        {
            _shutdown.Request();      // the stop, while the renewal waits on the ledger: the read observes its token
            throw new OperationCanceledException(_shutdown.Stopping);
        };

        await Run(steps: 1);
        var executed = _running!.ExecuteTask!;   // why: Run started the renewer, so both are set
        _ = await Task.WhenAny(executed);        // waits without rethrowing

        Assert.True(executed.IsCompletedSuccessfully, executed.Status.ToString());
        Assert.Equal(0, _exit.Code);
    }

    private LeaseRenewer Renewer() => new(_lease, _h.Held, _shutdown, _exit, _clock, _log);

    private async Task Run(int steps)
    {
        _running = Renewer();
        await _running.StartAsync(Token);
        for (var i = 0; i < steps; i++)
        {
            await Step();
        }
    }

    /// <summary>One renewal interval, then waits until the renewer finished that tick (armed the next, stopped, or returned).</summary>
    private async Task Step()
    {
        await _clock.Step(LeaseTiming.RenewEvery);
        await _clock.Armed(() => _shutdown.Requested || _running?.ExecuteTask?.IsCompleted == true);
    }
}
