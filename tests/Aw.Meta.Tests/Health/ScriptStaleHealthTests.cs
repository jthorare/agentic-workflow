using Aw.Meta.Core;
using Aw.Meta.Features.Health;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Health;

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

    private Task<string?> Warning() => new ScriptStaleCheck(_world.Runner, Paths).Warning(CancellationToken.None).AsTask();

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_heavy_gives_one_line_naming_both_paths_and_the_fix()
    {
        Hashes("heavy.sh", "aaa", "bbb");
        Hashes("meta-gate-reserve.sh", "ccc", "ccc");

        var line = await Warning();

        Assert.NotNull(line);
        Assert.DoesNotContain("\n", line, StringComparison.Ordinal);
        Assert.Contains("/state/bin-r1b/heavy", line, StringComparison.Ordinal);
        Assert.Contains("origin/main's scripts/heavy.sh", line, StringComparison.Ordinal);
        Assert.DoesNotContain("meta-gate-reserve", line, StringComparison.Ordinal);
        Assert.Contains("scripts/install-cs.sh", line, StringComparison.Ordinal);
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
    public async Task A_changed_meta_gate_reserve_gives_a_line()
    {
        Hashes("heavy.sh", "aaa", "aaa");
        Hashes("meta-gate-reserve.sh", "ccc", "ddd");

        var line = await Warning();

        Assert.NotNull(line);
        Assert.Contains("/state/bin-r1b/meta-gate-reserve", line, StringComparison.Ordinal);
        Assert.Contains("origin/main's scripts/meta-gate-reserve.sh", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Both_changed_still_gives_one_line()
    {
        Hashes("heavy.sh", "aaa", "bbb");
        Hashes("meta-gate-reserve.sh", "ccc", "ddd");

        var line = await Warning();

        Assert.NotNull(line);
        Assert.DoesNotContain("\n", line, StringComparison.Ordinal);
        Assert.Contains("heavy.sh", line, StringComparison.Ordinal);
        Assert.Contains("meta-gate-reserve.sh", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Matching_hashes_give_no_line()
    {
        Hashes("heavy.sh", "aaa", "aaa");
        Hashes("meta-gate-reserve.sh", "ccc", "ccc");

        Assert.Null(await Warning());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_git_cannot_hash_gives_no_line()
    {
        _ = _world.Runner.When("hash-object", exitCode: 128);
        _ = _world.Runner.When("rev-parse", stdout: "bbb\n");

        Assert.Null(await Warning());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_origin_main_gives_no_line()
    {
        _ = _world.Runner.When("hash-object", stdout: "aaa\n");
        _ = _world.Runner.When("rev-parse", exitCode: 128);

        Assert.Null(await Warning());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_or_unscripted_git_run_gives_no_line() => Assert.Null(await Warning());

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_checkout_nothing_is_compared()
    {
        var check = new ScriptStaleCheck(_world.Runner, Paths with { Toolkit = null });

        Assert.Null(await check.Warning(CancellationToken.None));
        Assert.Empty(_world.Runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Health_carries_the_line_without_raising_attention()
    {
        Hashes("heavy.sh", "aaa", "bbb");
        var service = new HealthService(
            new HostProbe(_world.Processes, _world.Resources, new OomPolicyCheck(_world.Runner, _world.Paths), new ScriptStaleCheck(_world.Runner, Paths)),
            new LaneCheck(_world.Config, new Core.Coordination.MissingLanes(_world.Sessions, _world.Messages, _world.Lanes), _world.LaneHealth, _world.Context),
            new OrphanKiller(_world.Processes, _world.Log, _world.Clock),
            _world.Values,
            _world.Config,
            Worlds.Advisor(_world),
            _world.Clock);

        var result = await service.Handle(new HealthRequest(KillOrphans: false), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Contains("origin/main's scripts/heavy.sh", result.Value.StaleScripts, StringComparison.Ordinal);
        Assert.False(result.Value.Attention);
    }
}
