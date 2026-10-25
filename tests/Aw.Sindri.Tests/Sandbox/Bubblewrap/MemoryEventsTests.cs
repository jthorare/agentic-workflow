using Aw.Kernel.Testing;
using Aw.Sindri.Testing;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>A scope's memory.events counters (sandbox spec §3.3, spike item 4): read while the scope is alive.</summary>
[Lane(TestLane.Unit)]
public sealed class MemoryEventsTests
{
    [Fact]
    public void The_four_counters_are_read_by_name() =>
        Expect.Equal(new MemoryEvents(High: 7, Max: 2, Oom: 1, OomKill: 1), MemoryEvents.Parse("low 0\nhigh 7\nmax 2\noom 1\noom_kill 1\noom_group_kill 0\n"));

    [Fact]
    public void A_missing_file_or_key_counts_zero()
    {
        Expect.Equal(MemoryEvents.None, MemoryEvents.Parse(null));
        Expect.Equal(new MemoryEvents(High: 0, Max: 3, Oom: 0, OomKill: 0), MemoryEvents.Parse("max 3\n"));
    }

    [Fact]
    public void A_line_that_is_not_a_number_is_skipped() =>
        Expect.Equal(new MemoryEvents(High: 0, Max: 0, Oom: 0, OomKill: 4), MemoryEvents.Parse("high many\n\nnonsense\noom_kill 4\n"));
}
