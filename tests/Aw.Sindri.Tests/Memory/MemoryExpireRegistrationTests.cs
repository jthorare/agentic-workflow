using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Memory;

/// <summary>The scheduler runs 'memory-expire' without a profile entry: it is a registered built-in with an hourly cadence, next to its scheduled service.</summary>
[Lane(TestLane.Unit)]
public sealed class MemoryExpireRegistrationTests
{
    [Fact]
    public void Memory_expire_is_a_registered_hourly_built_in_with_its_scheduled_service()
    {
        var services = new ServiceCollection().AddMemorySlices();

        var builtIn = Assert.Single(services.Where(static d => d.ServiceType == typeof(BuiltInService)).Select(static d => (BuiltInService)d.ImplementationInstance!));
        Assert.Equal("memory-expire", builtIn.Name.Value);
        Assert.Equal(TimeSpan.FromHours(1), builtIn.Cadence.Interval);
        Assert.Contains(services, static d => d.ServiceType == typeof(IScheduledService) && d.ImplementationType?.Name == "ExpireMemoryService");
    }
}
