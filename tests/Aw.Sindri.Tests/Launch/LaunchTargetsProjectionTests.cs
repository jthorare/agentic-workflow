using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>'GET /launch/targets' (UI spec E-3, screens spec E-28): the newest probe with each target's budget.</summary>
[Lane(TestLane.Unit)]
public sealed class LaunchTargetsProjectionTests
{
    private static readonly AsOf _asOf = new(Cursor: 7, LedgerHead: 7, ProjectedAt: null, Rebuilding: false, Stalled: false);
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly AgentId _codex = AgentId.From("codex");
    private static readonly DateTimeOffset _at = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private static LaunchProbed Probed() => new(
        new TerminalFacts("exec-template", "kitty", Present: true, "file", "/home/launcher.json", Problem: null, Detail: null),
        new HostFacts(HostKind.Tmux, Present: true, "3.4", Problem: null),
        [new TargetFacts(_claude, Present: true, "1.0", Limited: null, Problem: null, CatalogSource.Builtin, Overridden: false),
         new TargetFacts(_codex, Present: false, Version: null, Limited: "no-usage-reader", "not-installed", CatalogSource.Builtin, Overridden: false)],
        []);

    private static AgentSettings Agents() =>
        new(SoftLimit: null, new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { [_claude.Value] = new(DailyBudget: 10m) });

    [Fact]
    public void Before_the_first_probe_nothing_is_known()
    {
        var view = LaunchTargetsProjection.Project(probed: null, probedAt: null, agents: null, new Dictionary<string, AgentSpend>(StringComparer.Ordinal), _asOf);

        Assert.False(view.Probed);
        Assert.Empty(view.Targets);
        Assert.Null(view.Terminal);
        Expect.Equal(LaunchBudgetState.None, view.BudgetTotal.State);
    }

    [Fact]
    public void The_probe_is_served_with_each_targets_budget_in_catalog_order()
    {
        var spend = new Dictionary<string, AgentSpend>(StringComparer.Ordinal) { [_claude.Value] = new(8m, 10, SpendBasis.PriceTable) };

        var view = LaunchTargetsProjection.Project(Probed(), _at, Agents(), spend, _asOf);

        Assert.True(view.Probed);
        Expect.Equal((_at, "kitty", _asOf), (view.ProbedAt, view.Terminal!.Label, view.AsOf));
        Assert.Equal(["claude-code", "codex"], view.Targets.Select(t => t.Facts.Id.Value), StringComparer.Ordinal);
        Expect.Equal(LaunchBudgetState.Soft, view.Targets[0].Budget.State);
        Expect.Equal(LaunchBudgetState.None, view.Targets[1].Budget.State);   // no budget, no reader: nothing to say
        Expect.Equal((10m, 8m), (view.BudgetTotal.DailyUsd, view.BudgetTotal.SpentUsd));
    }

    [Fact]
    public void Usage_none_with_budget_is_010_no_usage_reader()
    {
        var agents = new AgentSettings(SoftLimit: null, new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { [_codex.Value] = new(DailyBudget: 5m) });

        var view = LaunchTargetsProjection.Project(Probed(), _at, agents, new Dictionary<string, AgentSpend>(StringComparer.Ordinal), _asOf);

        Expect.Equal(LaunchBudgetState.NoUsageReader, view.Targets[1].Budget.State);   // the state OpenItem and the launcher refuse as SND-LAUNCH-010
        Expect.Equal(LaunchBudgetState.NoUsageReader, LaunchBudgets.Of(agents, _codex, (AgentSpend?)null));
    }
}
