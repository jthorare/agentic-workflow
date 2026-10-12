using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Lease;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="ILease"/> honours (factory addendum §17.22 D2, design spec §9.1), run against the
/// in-memory store and the SQLite adapter. A derived class binds <see cref="Create"/> and carries the lane.
/// </summary>
public abstract class LeaseContract
{
    /// <summary>The contract's clock, starting at the known epoch.</summary>
    protected FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    /// <summary>The probe the store consults; this process is pid 100.</summary>
    protected FakeProcessProbe Probe { get; } = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The stores over a fresh, empty file or memory.</summary>
    /// <param name="clock">The only clock.</param>
    /// <param name="probe">The process probe.</param>
    /// <returns>The stores.</returns>
    protected abstract ValueTask<SindriStores> Create(TimeProvider clock, IProcessProbe probe);

    /// <summary>The first acquisition gets epoch 1 and the TTL.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task First_acquire_gets_epoch_one()
    {
        var lease = (await Create(Clock, Probe)).Lease;
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        Expect.Equal(1, grant.Epoch);
        Expect.Equal(KnownInstants.Epoch + LeaseTiming.Ttl, grant.ExpiresAt);
    }

    /// <summary>A second process is refused while the holder lives, and the refusal names the holder.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Second_acquire_is_refused_while_the_holder_lives()
    {
        var lease = (await Create(Clock, Probe)).Lease;
        _ = await lease.Acquire(Probe.Self(), Token);
        var other = FakeProcessProbe.Holder(200);
        Probe.Start(other);

        var refused = await lease.Acquire(other, Token);

        Expect.Equal(SindriErrors.LeaseHeld.Code, refused.Error.Code);
        Expect.True(refused.Error.Message.Contains("box/100", StringComparison.Ordinal));
    }

    /// <summary>A holder whose pid is gone is taken over at once, with the next epoch.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_dead_holder_is_taken_over_before_expiry()
    {
        var lease = (await Create(Clock, Probe)).Lease;
        var dead = FakeProcessProbe.Holder(300);
        Probe.Start(dead);
        _ = (await lease.Acquire(dead, Token)).Value;
        Probe.Kill(300);

        var grant = await lease.Acquire(Probe.Self(), Token);

        Expect.Equal(2, grant.Value.Epoch);
    }

    /// <summary>A pid reused by a later process doesn't keep a dead holder's lease.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_reused_pid_does_not_keep_the_lease()
    {
        var lease = (await Create(Clock, Probe)).Lease;
        var old = FakeProcessProbe.Holder(300);
        Probe.Start(old);
        _ = await lease.Acquire(old, Token);
        Probe.Start(FakeProcessProbe.Holder(300, KnownInstants.Epoch.AddMinutes(5)));

        Expect.True((await lease.Acquire(Probe.Self(), Token)).IsSuccess);
    }

    /// <summary>Releasing keeps the epoch, so the next holder gets epoch + 1.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Release_then_acquire_increments_the_epoch()
    {
        var lease = (await Create(Clock, Probe)).Lease;
        var first = (await lease.Acquire(Probe.Self(), Token)).Value;
        Expect.True((await lease.Release(first, Token)).IsSuccess);

        var second = (await lease.Acquire(Probe.Self(), Token)).Value;

        Expect.Equal(2, second.Epoch);
        Expect.True((await lease.Read(Token)).Value is { Epoch: 2, Released: false });
    }

    /// <summary>An expired lease of a holder on another machine goes to the next asker; the old holder's renew then fails.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_paused_holder_loses_on_renew_after_another_took_over()
    {
        var lease = (await Create(Clock, Probe)).Lease;
        var paused = FakeProcessProbe.Remote(400);
        var old = (await lease.Acquire(paused, Token)).Value;
        Clock.Advance(LeaseTiming.Ttl + TimeSpan.FromSeconds(1));
        _ = (await lease.Acquire(Probe.Self(), Token)).Value;

        var renew = await lease.Renew(old, Token);

        Expect.Equal(SindriErrors.LeaseLost.Code, renew.Error.Code);
    }

    /// <summary>An expired lease whose holder is alive on this machine is refused: its renewals are only late.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_expired_lease_of_a_holder_alive_on_this_machine_is_refused()
    {
        var lease = (await Create(Clock, Probe)).Lease;
        var late = FakeProcessProbe.Holder(500);
        Probe.Start(late);
        _ = (await lease.Acquire(late, Token)).Value;
        Clock.Advance(LeaseTiming.Ttl + TimeSpan.FromSeconds(1));

        var refused = await lease.Acquire(Probe.Self(), Token);

        Expect.Equal(SindriErrors.LeaseHeld.Code, refused.Error.Code);
    }

    /// <summary>A holder that slept past expiry, when nobody took over, renews and keeps its epoch.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_sleeper_renews_when_nobody_took_over()
    {
        var lease = (await Create(Clock, Probe)).Lease;
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        Clock.Advance(TimeSpan.FromMinutes(10));

        var renewed = (await lease.Renew(grant, Token)).Value;

        Expect.Equal(grant.Epoch, renewed.Epoch);
        Expect.Equal(Clock.GetUtcNow() + LeaseTiming.Ttl, renewed.ExpiresAt);
    }

    /// <summary>Releasing with a grant that isn't current is refused and changes nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Release_by_a_stale_grant_is_refused()
    {
        var lease = (await Create(Clock, Probe)).Lease;
        var first = (await lease.Acquire(Probe.Self(), Token)).Value;
        _ = await lease.Release(first, Token);
        var second = (await lease.Acquire(Probe.Self(), Token)).Value;

        var stale = await lease.Release(first, Token);

        Expect.Equal(SindriErrors.LeaseLost.Code, stale.Error.Code);
        Expect.True((await lease.Read(Token)).Value is { Released: false } row && row.Epoch == second.Epoch);
    }
}
