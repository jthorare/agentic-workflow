using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;

namespace Aw.Sindri.Tests.Sandbox.ProcessGroup;

/// <summary>A process group's memory on macOS is the system's phys footprint when it can be read, and the sum of resident sets when it cannot (SB-9c).</summary>
[Lane(TestLane.Unit)]
public sealed class ProcessGroupFootprintTests
{
    private const string Listing = "  100   500   2048  0:01.00\n  101   500   1024  0:00.50\n  200   900   99999  5:00.00\n";

    [Fact]
    public void The_footprint_replaces_the_resident_sum_and_is_asked_for_the_pids_of_the_group_only()
    {
        var footprint = new FakeFootprint(1_400_000_000L);
        var stats = new ProcessGroupStats(new FakeProcesses().When((_, _) => true, FakeProcesses.Ok(Listing)), footprint: footprint);

        var sample = stats.Read("pgid:500");

        Assert.NotNull(sample);
        Assert.Equal(1_400_000_000L, sample.MemoryCurrent);
        Assert.Equal(1_400_000_000L, sample.MemoryPeak);
        Assert.Equal(2, sample.Pids);
        Assert.Equal(1_500_000L, sample.CpuMicroseconds);
        Assert.Equal([100, 101], footprint.Asked.Single());
    }

    [Fact]
    public void A_footprint_that_cannot_be_read_leaves_the_resident_sum()
    {
        var stats = new ProcessGroupStats(new FakeProcesses().When((_, _) => true, FakeProcesses.Ok(Listing)), footprint: new FakeFootprint(answer: null));

        Assert.Equal(3 * 1024 * 1024L, stats.Read("pgid:500")?.MemoryCurrent);
    }

    private sealed class FakeFootprint(long? answer) : IPhysFootprint
    {
        internal List<int[]> Asked { get; } = [];

        public long? Sum(IReadOnlyList<int> pids)
        {
            Asked.Add([.. pids]);
            return answer;
        }
    }
}
