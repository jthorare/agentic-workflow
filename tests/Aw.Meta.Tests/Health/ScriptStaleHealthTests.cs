using Aw.Meta.Features.Health;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Health;

public sealed class ScriptStaleHealthTests
{
    private readonly MetaWorld _world = new();

    private Aw.Meta.Core.MetaPaths Paths => _world.Paths with { Heavy = "/state/bin-r1b/heavy", Reserve = "/state/bin-r1b/meta-gate-reserve", Toolkit = "/kit" };

    private Task<string?> Warning() => new ScriptStaleCheck(_world.Runner, Paths).Warning(CancellationToken.None).AsTask();

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_heavy_gives_one_line_naming_both_paths_and_the_fix()
    {
        _ = _world.Runner.When("scripts/heavy.sh", exitCode: 1);
        _ = _world.Runner.When("scripts/meta-gate-reserve.sh");

        var line = await Warning();

        Assert.NotNull(line);
        Assert.DoesNotContain("\n", line, StringComparison.Ordinal);
        Assert.Contains("/state/bin-r1b/heavy", line, StringComparison.Ordinal);
        Assert.Contains("/kit/scripts/heavy.sh", line, StringComparison.Ordinal);
        Assert.DoesNotContain("meta-gate-reserve", line, StringComparison.Ordinal);
        Assert.Contains("scripts/install-cs.sh", line, StringComparison.Ordinal);
        Assert.Equal(["cmp -s /state/bin-r1b/heavy /kit/scripts/heavy.sh", "cmp -s /state/bin-r1b/meta-gate-reserve /kit/scripts/meta-gate-reserve.sh"], _world.Runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_changed_meta_gate_reserve_gives_a_line()
    {
        _ = _world.Runner.When("scripts/meta-gate-reserve.sh", exitCode: 1);

        var line = await Warning();

        Assert.NotNull(line);
        Assert.Contains("/state/bin-r1b/meta-gate-reserve", line, StringComparison.Ordinal);
        Assert.Contains("/kit/scripts/meta-gate-reserve.sh", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Both_changed_still_gives_one_line()
    {
        _ = _world.Runner.When("cmp -s", exitCode: 1);

        var line = await Warning();

        Assert.NotNull(line);
        Assert.DoesNotContain("\n", line, StringComparison.Ordinal);
        Assert.Contains("heavy.sh", line, StringComparison.Ordinal);
        Assert.Contains("meta-gate-reserve.sh", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Identical_copies_give_no_line()
    {
        _ = _world.Runner.When("cmp -s");

        Assert.Null(await Warning());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_cmp_cannot_read_gives_no_line()
    {
        _ = _world.Runner.When("cmp -s", exitCode: 2);

        Assert.Null(await Warning());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_cmp_gives_no_line() => Assert.Null(await Warning());

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
        _ = _world.Runner.When("scripts/heavy.sh", exitCode: 1);
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
        Assert.Contains("/kit/scripts/heavy.sh", result.Value.StaleScripts, StringComparison.Ordinal);
        Assert.False(result.Value.Attention);
    }
}
