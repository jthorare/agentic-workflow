using Aw.Kernel.Testing;
using Aw.Sindri.Core.Health;

namespace Aw.Sindri.Tests.Health;

/// <summary>Agent runs idle past their launch's idle time in the health word (issue #1000): any make a healthy report degraded.</summary>
[Lane(TestLane.Unit)]
public sealed class HealthStalledAgentsTests
{
    private static readonly HealthReport _healthy = new(HealthWord.Healthy, [], Sentence: null);

    [Fact]
    public void None_stalled_leaves_the_report_as_it_was() => Assert.Same(_healthy, HealthSummary.WithStalledAgents(_healthy, 0));

    [Fact]
    public void One_stalled_run_makes_a_healthy_report_degraded_and_points_at_Needs_you()
    {
        var report = HealthSummary.WithStalledAgents(_healthy, 1);

        Assert.Equal(HealthWord.Degraded, report.Word);
        var problem = Assert.Single(report.Problems);
        Assert.Equal("SND-ENGINE-004", problem.Code);
        Assert.Equal("1 agent run ended its turn and has not reported", problem.Clause);
        Assert.Contains("open Needs you", problem.Text, StringComparison.Ordinal);
        Assert.Equal("Sindri is degraded: 1 agent run ended its turn and has not reported.", report.Sentence);
    }

    [Fact]
    public void Several_are_plural() => Assert.Equal("3 agent runs ended their turn and have not reported", Assert.Single(HealthSummary.WithStalledAgents(_healthy, 3).Problems).Clause);

    [Fact]
    public void A_worse_word_stays_and_the_problem_joins_the_stuck_ones()
    {
        var stuck = HealthSummary.WithStuck(new HealthReport(HealthWord.Stalled, [new HealthProblem("SND-VIEW-001", "x", "the views stopped")], "Sindri is stalled: the views stopped."), 1);

        var report = HealthSummary.WithStalledAgents(stuck, 2);

        Assert.Equal(HealthWord.Stalled, report.Word);
        Assert.Equal(["SND-VIEW-001", "SND-ENGINE-002", "SND-ENGINE-004"], report.Problems.Select(static p => p.Code), StringComparer.Ordinal);
    }
}
