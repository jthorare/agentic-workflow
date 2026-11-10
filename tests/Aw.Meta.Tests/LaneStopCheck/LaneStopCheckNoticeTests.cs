using Aw.Meta.Features.LaneStopCheck;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneStopCheck;

public sealed class LaneStopCheckNoticeTests
{
    private readonly MetaWorld _world = new();

    private LaneStopCheckService Service() => new(_world.Config, _world.LaneMail, _world.Beats, _world.LaneGuard, _world.Clock);

    private async Task<LaneStopCheckResponse> Check(Core.Lanes.LaneIdentity lane)
    {
        var result = await Service().Handle(new LaneStopCheckRequest(lane, HookActive: false, MaxBlocks: 3) { HookEvent = "Stop" }, CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("main moved to abc1234: #12 merged.")]
    [InlineData("LANE MAP v12: meta=a, api=b")]
    public async Task Lane_mail_holding_only_a_notice_does_not_block_the_stop(string notice)
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        _ = await _world.LaneMail.Send("api", notice, A.At(5), CancellationToken.None);

        var response = await Check(lane);

        Assert.False(response.Blocked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Lane_mail_asking_a_question_blocks_the_stop()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        _ = await _world.LaneMail.Send("api", "can you rerun the gate on #12?", A.At(5), CancellationToken.None);

        var response = await Check(lane);

        Assert.True(response.Blocked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_notice_beside_a_question_blocks_and_the_owed_list_shows_the_question()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        _ = await _world.LaneMail.Send("api", "main moved to abc1234: #12 merged.", A.At(5), CancellationToken.None);
        _ = await _world.LaneMail.Send("api", "can you rerun the gate on #12?", A.At(6), CancellationToken.None);

        var response = await Check(lane);

        Assert.True(response.Blocked);
        Assert.Contains(response.Owed, o => o.Text.Contains("rerun the gate", StringComparison.Ordinal));
    }
}
