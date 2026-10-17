using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.GetLaunchTargets;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetLaunchTargets;

/// <summary>GET /launch/targets (UI spec E-3, screens spec E-28): the newest probe with each target's budget and today's spend.</summary>
[Lane(TestLane.Unit)]
public sealed class GetLaunchTargetsTests
{
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly AgentId _codex = AgentId.From("codex");
    private readonly ViewsHarness _v = new();
    private readonly FixedLaunchSpend _spend = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetLaunchTargetsService Service() => new(_v.Slice.Store, _v.Slice.Events, _v.Slice.Profiles, _spend, _v.Progress);

    private async Task Probed(params TargetFacts[] targets)
    {
        var grant = await _v.Slice.HoldLease();
        var probed = new LaunchProbed(
            new TerminalFacts("print", "Print", Present: true, "detected", ConfigPath: null, Problem: null, Detail: null),
            new HostFacts(HostKind.Tmux, Present: true, "3.4", Problem: null), targets, []);
        _ = await _v.Slice.Store.Append(_v.Slice.Events.Entry(EventTypes.LaunchProbed, probed, "launch.probed:0"), grant, Token);
    }

    private static TargetFacts Target(AgentId id) => new(id, Present: true, "1.0", Limited: null, Problem: null, CatalogSource.Builtin, Overridden: false);

    private async Task Approve(decimal budget)
    {
        var agents = new AgentSettings(SoftLimit: null, new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { [_claude.Value] = new(DailyBudget: budget) });
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Agents: agents)).Value;
        _ = await _v.Slice.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), Token);
    }

    [Fact]
    public async Task GetLaunchTargets_returns_the_probed_targets()
    {
        await Probed(Target(_claude), Target(_codex));

        var view = (await Service().Handle(A.GetLaunchTargetsRequest().Build(), Token)).Value;

        Assert.True(view.Probed);
        Assert.Equal(["claude-code", "codex"], view.Targets.Select(static t => t.Facts.Id.Value), StringComparer.Ordinal);
        Expect.Equal(_v.Progress.Current.AsOf, view.AsOf);
    }

    [Fact]
    public async Task Before_any_probe_the_view_says_so()
    {
        var view = (await Service().Handle(A.GetLaunchTargetsRequest().Build(), Token)).Value;

        Assert.False(view.Probed);
        Assert.Empty(view.Targets);
    }

    [Fact]
    public async Task A_target_over_its_budget_reads_hard_and_another_without_one_reads_none()
    {
        await Probed(Target(_claude), Target(_codex));
        await Approve(10m);
        _spend.Spent(_claude, 10m);
        _spend.Spent(_codex, 3m);

        var view = (await Service().Handle(A.GetLaunchTargetsRequest().Build(), Token)).Value;

        Expect.Equal((LaunchBudgetState.Hard, 10m), (view.Targets[0].Budget.State, view.Targets[0].Budget.SpentUsd!.Value));
        Expect.Equal((LaunchBudgetState.None, 3m), (view.Targets[1].Budget.State, view.Targets[1].Budget.SpentUsd!.Value));
        Expect.Equal((10m, 13m), (view.BudgetTotal.DailyUsd, view.BudgetTotal.SpentUsd));
    }

    [Fact]
    public async Task A_budget_for_a_target_with_no_reader_reads_no_usage_reader()
    {
        await Probed(Target(_claude));
        await Approve(10m);

        var view = (await Service().Handle(A.GetLaunchTargetsRequest().Build(), Token)).Value;

        Expect.Equal(LaunchBudgetState.NoUsageReader, view.Targets[0].Budget.State);
    }
}
