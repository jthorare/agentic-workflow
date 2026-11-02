using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>Issue 1005: the scoping agents work from the prefiltered graph (fact:graph), never the raw lenses. Check, Evidence and the gap judge are block Steps and may keep reading fact:lenses.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopingAgentReadsTests
{
    private static string[] Reads(BlockDescriptor step) => [.. step.Inputs.Select(static f => f.Value)];

    private static IEnumerable<BlockDescriptor> AgentSteps() =>
        new[] { typeof(ScopingSteps), typeof(GapSearchSteps), typeof(RelevanceSteps) }
            .SelectMany(static t => t.GetProperties(BindingFlags.Public | BindingFlags.Static))
            .Where(static p => p.PropertyType == typeof(BlockDescriptor))
            .Select(static p => (BlockDescriptor)p.GetValue(null)!)
            .Where(static d => d.Runtime == BlockRuntime.Agent);

    [Fact]
    public void The_exclude_step_reads_the_scoping_graph_and_not_the_raw_lenses()
    {
        var reads = Reads(ScopingSteps.Exclude);

        Assert.Contains("fact:graph", reads, StringComparer.Ordinal);
        Assert.DoesNotContain("fact:lenses", reads, StringComparer.Ordinal);
    }

    [Fact]
    public void No_scoping_agent_step_reads_the_raw_lenses()
    {
        var agents = AgentSteps().ToArray();

        Assert.NotEmpty(agents);
        Assert.Empty(agents.Where(static d => Reads(d).Contains("fact:lenses", StringComparer.Ordinal)).Select(static d => d.Id.Value));
    }
}
