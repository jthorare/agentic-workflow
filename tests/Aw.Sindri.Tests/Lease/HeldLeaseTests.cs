using Aw.Kernel.Testing;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Lease;

public sealed class HeldLeaseTests
{
    private static readonly LeaseGrant _grant = new(FakeProcessProbe.Holder(100), 3, KnownInstants.Epoch.AddMinutes(1));

    [Fact, Trait("Lane", "unit")]
    public void Replace_holds_the_renewed_grant_while_the_old_one_is_current()
    {
        var held = new HeldLease();
        held.Hold(_grant);
        var renewed = _grant with { ExpiresAt = _grant.ExpiresAt.AddMinutes(1) };

        Assert.True(held.Replace(_grant, renewed));
        Assert.Same(renewed, held.Current);
    }

    [Fact, Trait("Lane", "unit")]
    public void Replace_after_a_stop_cleared_the_grant_does_not_bring_it_back()
    {
        var held = new HeldLease();
        held.Hold(_grant);
        held.Clear();

        Assert.False(held.Replace(_grant, _grant with { ExpiresAt = _grant.ExpiresAt.AddMinutes(1) }));
        Assert.Null(held.Current);
    }
}
