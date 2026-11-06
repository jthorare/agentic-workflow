using Aw.Meta.Core.Lanes;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Health;

public sealed class LaneHealthTests
{
    private readonly MetaWorld _world = new();

    private async Task<IReadOnlyList<LaneHealth>> Read() =>
        (await _world.LaneHealth.Read((await _world.Config.Load(CancellationToken.None)).Value, CancellationToken.None)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task A_team_of_Claude_lanes_has_no_rows()
    {
        await _world.Ready();
        _world.LaneFiles.Put(new LaneRecord("api", Core.Ids.SessionId.From("s"), new LaneAgent(LaneAgent.ClaudeCode, "claude", Hooks: true)));

        Assert.Empty(await Read());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_whose_hook_has_not_run_since_its_session_was_recorded_says_to_trust_the_hooks_in_codex()
    {
        await _world.Ready();
        _ = _world.PutLane("api");
        _ = await _world.Beats.Record("api", stopped: false, A.At(5), CancellationToken.None);   // before the session id was recorded at minute 10

        var row = Assert.Single(await Read());

        Assert.Equal(("api", "codex", "hooks never ran: trust them with /hooks in codex (each install that changes a hook asks again)", true), (row.Lane, row.Agent, row.State, row.Attention));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_whose_hook_ran_is_taking_part()
    {
        await _world.Ready();
        _ = _world.PutLane("api");
        _ = await _world.Beats.Record("api", stopped: false, A.At(15), CancellationToken.None);

        Assert.Equal(("taking part", false), (Assert.Single(await Read()).State, Assert.Single(await Read()).Attention));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_on_a_CLI_with_no_hooks_is_launch_only_and_needs_no_attention()
    {
        await _world.Ready();
        _ = _world.PutLane("ui", agent: "aider", hooks: false);

        var row = Assert.Single(await Read());

        Assert.Equal(("launch-only: no obligations", false), (row.State, row.Attention));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_ambiguous_session_id_is_shown()
    {
        await _world.Ready();
        _world.LaneFiles.Put(new LaneRecord("api", Session: null, new LaneAgent("codex", "codex", Hooks: true), RecordedAt: null, Ambiguous: true));

        Assert.Equal("ambiguous session id", Assert.Single(await Read()).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cursor_lane_blocked_as_often_as_Cursors_loop_limit_shows_the_loop_limit()
    {
        await _world.Ready();
        var lane = _world.PutLane("api", agent: "cursor");
        _ = await _world.Beats.Record("api", stopped: false, A.At(15), CancellationToken.None);
        _ = await _world.LaneMail.Send("api", "x", A.At(16), CancellationToken.None);
        _ = await _world.LaneGuard.Decide(new LaneStop(lane, (await _world.LaneMail.Owed("api", CancellationToken.None)).Value, HookActive: false, MaxBlocks: 99, A.At(17)), CancellationToken.None);
        for (var i = 0; i < LaneHealthReader_LoopLimit; i++)
        {
            _ = await _world.LaneGuard.Decide(new LaneStop(lane, (await _world.LaneMail.Owed("api", CancellationToken.None)).Value, HookActive: true, MaxBlocks: 99, A.At(18)), CancellationToken.None);
        }

        Assert.Equal("obligation not delivered (Cursor loop_limit)", Assert.Single(await Read()).State);
    }

    private const int LaneHealthReader_LoopLimit = Core.Coordination.LaneHealthReader.CursorLoopLimit;
}
