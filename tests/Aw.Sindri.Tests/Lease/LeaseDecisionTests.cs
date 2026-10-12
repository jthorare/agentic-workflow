using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Lease;

public sealed class LeaseDecisionTests
{
    private static readonly DateTimeOffset _now = KnownInstants.Epoch;
    private static readonly LeaseHolder _me = FakeProcessProbe.Holder(100);
    private static readonly LeaseHolder _other = FakeProcessProbe.Holder(200);

    [Fact, Trait("Lane", "unit")]
    public void An_unexpired_lease_of_a_live_holder_is_refused_with_who_and_since()
    {
        var row = new LeaseRow(_other, 4, _now.AddSeconds(-10), _now.AddSeconds(50), Released: false);

        var refused = LeaseDecision.Acquire(row, _me, _now, HolderLiveness.Alive);

        Assert.Equal(SindriErrors.LeaseHeld.Code, refused.Error.Code);
        Assert.Equal("held by box/200 since 2025-12-31T23:59:50Z (epoch 4)", refused.Error.Message);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false, HolderLiveness.Unknown)]     // expired, on another machine
    [InlineData(false, HolderLiveness.ProvenDead)]  // expired and provably dead
    [InlineData(true, HolderLiveness.ProvenDead)]   // unexpired but provably dead
    public void A_lapsed_or_dead_holder_gives_the_next_epoch(bool live, HolderLiveness liveness)
    {
        var row = new LeaseRow(_other, 4, _now.AddSeconds(-70), live ? _now.AddSeconds(50) : _now.AddSeconds(-10), Released: false);

        var grant = LeaseDecision.Acquire(row, _me, _now, liveness).Value;

        Assert.Equal(new LeaseGrant(_me, 5, _now + LeaseTiming.Ttl), grant);
    }

    [Fact, Trait("Lane", "unit")]
    public void Unknown_liveness_waits_for_expiry()
    {
        var row = new LeaseRow(_other, 1, _now, _now.AddSeconds(1), Released: false);
        Assert.False(LeaseDecision.Acquire(row, _me, _now, HolderLiveness.Unknown).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_expired_lease_of_a_holder_alive_on_this_machine_is_refused()
    {
        var row = new LeaseRow(_other, 4, _now.AddSeconds(-70), _now.AddSeconds(-10), Released: false);

        Assert.Equal(SindriErrors.LeaseHeld.Code, LeaseDecision.Acquire(row, _me, _now, HolderLiveness.Alive).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_released_lease_is_free_whatever_its_holder_is()
    {
        var row = new LeaseRow(_other, 4, _now.AddSeconds(-10), _now, Released: true);

        Assert.Equal(5, LeaseDecision.Acquire(row, _me, _now, HolderLiveness.Alive).Value.Epoch);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("box", "boot-1", true, 0, HolderLiveness.Alive)]
    [InlineData("box", "boot-1", true, 2, HolderLiveness.Alive)]           // start-time jitter within 2 s: the same process
    [InlineData("box", "boot-1", true, -2, HolderLiveness.Alive)]
    [InlineData("box", "boot-1", true, 3, HolderLiveness.ProvenDead)]      // beyond it: a later process
    [InlineData("box", "boot-1", false, 0, HolderLiveness.ProvenDead)]     // pid gone
    [InlineData("box", "boot-1", true, 30, HolderLiveness.ProvenDead)]     // pid reused by a later process
    [InlineData("box", "boot-2", true, 0, HolderLiveness.ProvenDead)]      // another boot
    [InlineData("other-box", "boot-1", false, 0, HolderLiveness.Unknown)]  // another machine: nothing provable
    public void Liveness_follows_host_boot_pid_and_start(string host, string boot, bool running, int startShift, HolderLiveness expected)
    {
        var holder = new LeaseHolder(HostId.From(host), 200, _now, BootId.From(boot));
        var facts = new ProcessFacts(Running: running, StartedAt: running ? _now.AddSeconds(startShift) : null);

        Assert.Equal(expected, LeaseDecision.Liveness(holder, _me, facts));
    }

    [Fact, Trait("Lane", "unit")]
    public void Holds_is_false_after_release_or_for_another_epoch()
    {
        var grant = new LeaseGrant(_me, 3, _now.AddSeconds(60));
        var row = new LeaseRow(_me, 3, _now, _now.AddSeconds(60), Released: false);

        Assert.True(LeaseDecision.Holds(row, grant));
        Assert.False(LeaseDecision.Holds(row with { Released = true }, grant));
        Assert.False(LeaseDecision.Holds(row with { Epoch = 4 }, grant));
        Assert.False(LeaseDecision.Holds(row: null, grant));
    }
}
