using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ProbeLaunchTargets;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.ProbeLaunchTargets;

/// <summary>Checking the launch targets again (UI spec E-3): 'launch.probed' is appended only when what the probe found differs from the newest one.</summary>
[Lane(TestLane.Unit)]
public sealed class ProbeLaunchTargetsTests
{
    private readonly SliceHarness _slice = new();
    private readonly FakeLauncher _launcher = new();
    private readonly FakeRepoLocator _repos = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ProbeLaunchTargetsService> Service()
    {
        _ = await _slice.HoldLease();
        return new ProbeLaunchTargetsService(_slice.Held, new LaunchProbeRecorder(_launcher, _slice.Profiles, _repos, _slice.Store, _slice.Events));
    }

    private static TargetFacts Target(string id, bool present, string? limited = null) =>
        new(AgentId.From(id), present, present ? "1.0" : null, limited, present ? null : "not-installed", CatalogSource.Builtin, Overridden: false);

    private IReadOnlyList<Core.Ledger.LedgerEvent> Probes => [.. _slice.Store.Events.Where(static e => string.Equals(e.Envelope.Type, EventTypes.LaunchProbed, StringComparison.Ordinal))];

    [Fact]
    public async Task ProbeLaunchTargets_records_a_probe()
    {
        _launcher.Probed = _launcher.Probed with { Targets = [Target("claude-code", present: true), Target("codex", present: false)] };
        var service = await Service();

        var result = await service.Handle(A.ProbeLaunchTargetsRequest().Build(), Token);

        Expect.Equal(new ProbeLaunchTargetsResult(Changed: true, Targets: 2, Present: 1), result.Value);
        var recorded = _slice.Events.Payload<LaunchProbed>(Assert.Single(Probes)).Value;
        Assert.Equal(["claude-code", "codex"], recorded.Targets.Select(static t => t.Id.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Probe_appends_only_on_change()
    {
        var service = await Service();
        _launcher.Probed = _launcher.Probed with { Targets = [Target("claude-code", present: true)] };

        var first = (await service.Handle(A.ProbeLaunchTargetsRequest().Build(), Token)).Value;
        var again = (await service.Handle(A.ProbeLaunchTargetsRequest().Build(), Token)).Value;
        _launcher.Probed = _launcher.Probed with { Targets = [Target("claude-code", present: false)] };
        var changed = (await service.Handle(A.ProbeLaunchTargetsRequest().Build(), Token)).Value;
        _launcher.Probed = _launcher.Probed with { Targets = [Target("claude-code", present: true)] };
        var back = (await service.Handle(A.ProbeLaunchTargetsRequest().Build(), Token)).Value;

        Expect.Equal((true, false, true, true), (first.Changed, again.Changed, changed.Changed, back.Changed));
        Expect.Equal(3, Probes.Count);   // the state that came back is a new fact, not a replay of the first
    }

    [Fact]
    public async Task Probed_repos_come_from_the_approved_profile()
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Repos: [new RepoEntry("demo", "/work/demo"), new RepoEntry("gone", "/work/gone")])).Value;
        _ = await _slice.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(Kernel.Json.AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), Token);
        _ = _repos.In("/work/demo", "/work/demo");
        var service = await Service();

        _ = await service.Handle(A.ProbeLaunchTargetsRequest().Build(), Token);

        var recorded = _slice.Events.Payload<LaunchProbed>(Assert.Single(Probes)).Value;
        Assert.Equal([("demo", true), ("gone", false)], recorded.Repos.Select(static r => (r.Repo.Value, r.Present)));
    }

    [Fact]
    public async Task Without_the_lease_the_probe_is_refused_and_nothing_is_recorded()
    {
        var service = new ProbeLaunchTargetsService(_slice.Held, new LaunchProbeRecorder(_launcher, _slice.Profiles, _repos, _slice.Store, _slice.Events));

        var result = await service.Handle(A.ProbeLaunchTargetsRequest().Build(), Token);

        Assert.True(result.IsError);
        Assert.Empty(Probes);
    }
}
