using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ExpireMemory;
using Aw.Sindri.Testing.Memory;

namespace Aw.Sindri.Tests.Memory;

[Lane(TestLane.Unit)]
public sealed class ExpireMemoryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Expired_facts_are_deleted_by_the_hourly_service()
    {
        var slices = new SliceHarness();
        var grant = await slices.HoldLease();
        var h = new MemoryHarness();
        await h.Memory();
        _ = await h.Store.Add(MemorySamples.Fact("old", "old", expiresAt: h.Clock.GetUtcNow().AddMinutes(30)), Token);
        _ = await h.Store.Add(MemorySamples.Fact("keep", "keep"), Token);
        h.Clock.Advance(TimeSpan.FromHours(1));

        var ran = await h.Expire().Run(new ScheduledRun(grant, h.Clock.GetUtcNow()), Token);

        Assert.True(ran.IsSuccess);
        Assert.Equal(["keep"], h.Store.AllFacts.Select(static f => f.Key));
    }

    [Fact]
    public async Task The_service_is_a_built_in_named_memory_expire_that_runs_every_hour()
    {
        var h = new MemoryHarness();
        var slices = new SliceHarness();
        _ = await h.Expire().Run(new ScheduledRun(await slices.HoldLease(), h.Clock.GetUtcNow()), Token);   // red until the service runs

        Assert.Equal("memory-expire", ExpireMemoryService.BuiltIn.Name.Value);
        Assert.Equal(TimeSpan.FromHours(1), ExpireMemoryService.BuiltIn.Cadence.Interval);
    }
}
