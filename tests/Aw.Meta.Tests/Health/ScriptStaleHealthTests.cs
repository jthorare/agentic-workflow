using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Features.Health;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Health;

// #3698: the installed heavy and meta-gate-reserve against origin/main's copies; #4249: one drift per script, unknown when it can't be compared.
public sealed class ScriptStaleHealthTests
{
    private readonly MetaWorld _world = new();

    private MetaPaths Paths => _world.Paths with { Heavy = "/state/bin-r1b/heavy", Reserve = "/state/bin-r1b/meta-gate-reserve", Toolkit = "/kit" };

    // Scripts the two git calls for one script: hash-object of the installed file, rev-parse of origin/main's copy.
    private void Hashes(string script, string installedHash, string mainHash)
    {
        _ = _world.Runner.When($"rev-parse origin/main:scripts/{script}", stdout: mainHash + "\n");
        _ = _world.Runner.When($"hash-object /state/bin-r1b/{script.Replace(".sh", string.Empty, StringComparison.Ordinal)}", stdout: installedHash + "\n");
    }

    private Task<IReadOnlyList<ToolDrift>> Drifts(MetaPaths? paths = null) => new ScriptStaleCheck(_world.Runner, paths ?? Paths).Drifts(CancellationToken.None).AsTask();

    private static ToolDrift Of(IReadOnlyList<ToolDrift> drifts, string tool) => Assert.Single(drifts, d => d.Tool == tool);

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_heavy_is_stale_naming_both_paths_and_the_fix()
    {
        Hashes("heavy.sh", "aaa", "bbb");
        Hashes("meta-gate-reserve.sh", "ccc", "ccc");

        var drifts = await Drifts();

        var heavy = Of(drifts, "heavy");
        Assert.Equal(DriftState.Stale, heavy.State);
        Assert.DoesNotContain("\n", heavy.Line(), StringComparison.Ordinal);
        Assert.Contains("/state/bin-r1b/heavy", heavy.Detail, StringComparison.Ordinal);
        Assert.Contains("origin/main's scripts/heavy.sh", heavy.Detail, StringComparison.Ordinal);
        Assert.Contains("scripts/install-cs.sh", heavy.Detail, StringComparison.Ordinal);
        Assert.Equal(DriftState.Current, Of(drifts, "meta-gate-reserve").State);
        Assert.Equal(
            [
                "git -C /kit hash-object /state/bin-r1b/heavy",
                "git -C /kit rev-parse origin/main:scripts/heavy.sh",
                "git -C /kit hash-object /state/bin-r1b/meta-gate-reserve",
                "git -C /kit rev-parse origin/main:scripts/meta-gate-reserve.sh",
            ],
            _world.Runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_meta_gate_reserve_is_stale()
    {
        Hashes("heavy.sh", "aaa", "aaa");
        Hashes("meta-gate-reserve.sh", "ccc", "ddd");

        var reserve = Of(await Drifts(), "meta-gate-reserve");

        Assert.Equal(DriftState.Stale, reserve.State);
        Assert.Contains("origin/main's scripts/meta-gate-reserve.sh", reserve.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Matching_hashes_are_current()
    {
        Hashes("heavy.sh", "aaa", "aaa");
        Hashes("meta-gate-reserve.sh", "ccc", "ccc");

        Assert.All(await Drifts(), d => Assert.Equal(DriftState.Current, d.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_git_cannot_hash_is_unknown_with_the_fix()
    {
        _ = _world.Runner.When("hash-object", exitCode: 128);
        _ = _world.Runner.When("rev-parse", stdout: "bbb\n");

        var heavy = Of(await Drifts(), "heavy");

        Assert.Equal(DriftState.Unknown, heavy.State);
        Assert.Contains("scripts/install-cs.sh --publish --link", heavy.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_origin_main_is_unknown_never_current()
    {
        _ = _world.Runner.When("hash-object", stdout: "aaa\n");
        _ = _world.Runner.When("rev-parse", exitCode: 128);

        Assert.All(await Drifts(), d => Assert.Equal(DriftState.Unknown, d.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_or_unscripted_git_run_is_unknown() => Assert.All(await Drifts(), d => Assert.Equal(DriftState.Unknown, d.State));

    [Fact, Trait("Lane", "unit")]
    public async Task A_script_the_paths_dont_name_is_not_compared()
    {
        Hashes("heavy.sh", "aaa", "aaa");

        var drifts = await Drifts(Paths with { Reserve = null });

        Assert.Equal(["heavy"], drifts.Select(d => d.Tool));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_checkout_nothing_is_compared()
    {
        Assert.Empty(await Drifts(Paths with { Toolkit = null }));
        Assert.Empty(_world.Runner.Lines);
    }

    // #4249: a stale tool needs acting on, so health raises attention for it (exit 1), where #3698 only printed a line.
    [Fact, Trait("Lane", "unit")]
    public async Task Health_carries_a_stale_script_and_raises_attention()
    {
        Hashes("heavy.sh", "aaa", "bbb");
        var service = new HealthService(
            new HostProbe(_world.Processes, _world.Resources, new OomPolicyCheck(_world.Runner, _world.Paths), Worlds.Drift(_world, Paths)),
            new LaneCheck(_world.Config, new Core.Coordination.MissingLanes(_world.Sessions, _world.Messages, _world.Lanes), _world.LaneHealth, _world.Context),
            new OrphanKiller(_world.Processes, _world.Log, _world.Clock),
            _world.Values,
            _world.Config,
            Worlds.Advisor(_world),
            _world.Clock);

        var result = await service.Handle(new HealthRequest(KillOrphans: false), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.NotNull(result.Value.Drift);
        Assert.Contains(result.Value.Drift, d => d is { Tool: "heavy", State: DriftState.Stale });
        Assert.True(result.Value.Attention);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Health_without_a_checkout_has_no_drift_and_no_attention_from_it()
    {
        var service = new HealthService(
            new HostProbe(_world.Processes, _world.Resources, new OomPolicyCheck(_world.Runner, _world.Paths), Worlds.Drift(_world)),
            new LaneCheck(_world.Config, new Core.Coordination.MissingLanes(_world.Sessions, _world.Messages, _world.Lanes), _world.LaneHealth, _world.Context),
            new OrphanKiller(_world.Processes, _world.Log, _world.Clock),
            _world.Values,
            _world.Config,
            Worlds.Advisor(_world),
            _world.Clock);

        var result = await service.Handle(new HealthRequest(KillOrphans: false), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Null(result.Value.Drift);
        Assert.False(result.Value.Attention);
    }
}
