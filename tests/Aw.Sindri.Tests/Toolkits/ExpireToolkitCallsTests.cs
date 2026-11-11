using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features;
using Aw.Sindri.Features.ExpireToolkitCalls;
using Aw.Sindri.Testing.Toolkits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Toolkits;

[Lane(TestLane.Unit)]
public sealed class ExpireToolkitCallsTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Calls_older_than_30_days_are_deleted_and_newer_ones_kept()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var store = new InMemoryToolkitCalls();
        var now = clock.GetUtcNow();
        _ = await store.Add(ToolkitCallsContract.Sample("notes", "ancient", 0) with { At = now.AddDays(-31) }, Cancel);
        _ = await store.Add(ToolkitCallsContract.Sample("notes", "recent", 0) with { At = now.AddDays(-29) }, Cancel);
        var grant = await new SliceHarness().HoldLease();

        var ran = await new ExpireToolkitCallsService(store, clock).Run(new ScheduledRun(grant, now), Cancel);

        Assert.True(ran.IsSuccess);
        Assert.Equal(["recent"], store.All.Select(static c => c.Tool), StringComparer.Ordinal);
    }

    [Fact]
    public void Toolkit_calls_expire_is_a_registered_hourly_built_in_with_its_scheduled_service()
    {
        var services = new ServiceCollection().AddToolkitCallSlices();

        var builtIn = Assert.Single(services.Where(static d => d.ServiceType == typeof(BuiltInService)).Select(static d => (BuiltInService)d.ImplementationInstance!));
        Assert.Equal("toolkit-calls-expire", builtIn.Name.Value);
        Assert.Equal(TimeSpan.FromHours(1), builtIn.Cadence.Interval);
        Assert.Contains(services, static d => d.ServiceType == typeof(IScheduledService) && d.ImplementationType?.Name == "ExpireToolkitCallsService");
    }
}
