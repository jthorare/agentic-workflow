using Aw.Kernel.Testing;
using Aw.Sindri.Core.Health;

namespace Aw.Sindri.Tests.Health;

/// <summary>Agent sessions that sat idle and were ended with no report in the health word (issue #1265): any make a healthy report degraded.</summary>
[Lane(TestLane.Unit)]
public sealed class HealthIdleAgentsTests
{
    private static readonly HealthReport _healthy = new(HealthWord.Healthy, [], Sentence: null);

    [Fact]
    public void None_idle_leaves_the_report_as_it_was() => Assert.Same(_healthy, HealthSummary.WithIdleAgents(_healthy, 0));

    [Fact]
    public void One_idle_agent_makes_a_healthy_report_degraded_and_says_idle_with_no_report()
    {
        var report = HealthSummary.WithIdleAgents(_healthy, 1);

        Assert.Equal(HealthWord.Degraded, report.Word);
        var problem = Assert.Single(report.Problems);
        Assert.Equal(("SND-ENGINE-005", "1 agent idle, no report"), (problem.Code, problem.Clause));
        Assert.Equal("Sindri is degraded: 1 agent idle, no report.", report.Sentence);
    }

    [Fact]
    public void Several_are_plural_and_join_the_live_stalled_count_as_a_separate_problem()
    {
        var report = HealthSummary.WithIdleAgents(HealthSummary.WithStalledAgents(_healthy, 1), 2);

        Assert.Equal(["SND-ENGINE-004", "SND-ENGINE-005"], report.Problems.Select(static p => p.Code), StringComparer.Ordinal);
        Assert.Equal("2 agents idle, no report", report.Problems[1].Clause);
    }
}
