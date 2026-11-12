using Aw.Meta.Core.Commands;
using Aw.Meta.Features.Health;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.Health;

public sealed class OomPolicyHealthTests
{
    private readonly MetaWorld _world = new();

    private Task<string?> Warning() => new OomPolicyCheck(_world.Runner).Warning(CancellationToken.None).AsTask();

    [Fact, Trait("Lane", "unit")]
    public async Task A_stop_policy_gives_the_warning_line()
    {
        _ = _world.Runner.When("DefaultOOMPolicy", "stop\n");

        var line = await Warning();

        Assert.NotNull(line);
        Assert.Contains("DefaultOOMPolicy is stop", line, StringComparison.Ordinal);
        Assert.Contains("scripts/install-meta.sh", line, StringComparison.Ordinal);
        Assert.Equal("systemctl --user show -p DefaultOOMPolicy --value", Assert.Single(_world.Runner.Lines));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_continue_policy_gives_no_line()
    {
        _ = _world.Runner.When("DefaultOOMPolicy", "continue\n");

        Assert.Null(await Warning());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_call_gives_no_line()
    {
        _ = _world.Runner.When("DefaultOOMPolicy", "stop\n", exitCode: 1);

        Assert.Null(await Warning());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_timed_out_call_gives_no_line()
    {
        _ = _world.Runner.When("DefaultOOMPolicy", _ => new CommandResult(-1, "stop\n", string.Empty, TimedOut: true));

        Assert.Null(await Warning());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_systemctl_gives_no_line() => Assert.Null(await Warning());

    [Fact, Trait("Lane", "unit")]
    public async Task Health_carries_the_line_without_raising_attention()
    {
        _ = _world.Runner.When("DefaultOOMPolicy", "stop\n");
        var service = new HealthService(
            new HostProbe(_world.Processes, _world.Resources, new OomPolicyCheck(_world.Runner)),
            new LaneCheck(_world.Config, new Core.Coordination.MissingLanes(_world.Sessions, _world.Messages, _world.Lanes), _world.LaneHealth, _world.Context),
            new OrphanKiller(_world.Processes, _world.Log, _world.Clock),
            _world.Values,
            _world.Config,
            Worlds.Advisor(_world),
            _world.Clock);

        var result = await service.Handle(new HealthRequest(KillOrphans: false), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.Contains("DefaultOOMPolicy is stop", result.Value.OomPolicy, StringComparison.Ordinal);
        Assert.False(result.Value.Attention);
    }
}
