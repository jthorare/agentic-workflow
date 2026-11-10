using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneContexts;

public sealed class LaneContextReaderTests
{
    private readonly MetaWorld _world = new();

    private async Task<IReadOnlyList<LaneContext>> Read(int? recycleAt = null)
    {
        var team = new TeamConfig("t1", "/work", ["meta", "api", "ui"], "meta") { Context = new ContextConfig(recycleAt ?? 300000) };
        var rows = await _world.Context.Read(team, CancellationToken.None);
        Assert.True(rows.IsSuccess);
        return rows.Value;
    }

    // The rows of the work lanes: the meta lane's own row is checked by the first test.
    private async Task<IReadOnlyList<LaneContext>> Work(int? recycleAt = null) =>
        [.. (await Read(recycleAt)).Where(r => !string.Equals(r.Lane, "meta", StringComparison.Ordinal))];

    private void Claude(string lane, string session) => _world.LaneFiles.Put(new LaneRecord(lane, SessionId.From(session), Agent: null, A.At(1)));

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_under_the_threshold_is_ok_and_one_at_it_is_recycle_and_the_meta_lane_is_measured_too()
    {
        Claude("meta", "s-meta");
        Claude("api", "s-api");
        Claude("ui", "s-ui");
        _world.ClaudeTranscripts.PutUsage("s-meta", 310_000, A.At(4));
        _world.ClaudeTranscripts.PutUsage("s-api", 299_999, A.At(5));
        _world.ClaudeTranscripts.PutUsage("s-ui", 300_000, A.At(6));

        var rows = await Read();

        Assert.Equal(["meta", "api", "ui"], rows.Select(r => r.Lane), StringComparer.Ordinal);
        Assert.Equal([ContextVerdict.Recycle, ContextVerdict.Ok, ContextVerdict.Recycle], rows.Select(r => r.Verdict));
        Assert.Equal(A.At(6), rows[2].LastActivity);
        Assert.Equal("300k", rows[2].Compact());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_live_session_id_wins_over_the_recorded_one_because_a_clear_changes_it()
    {
        Claude("api", "s-old");
        _world.ClaudeTranscripts.PutUsage("s-old", 480_000, A.At(5));
        _world.ClaudeTranscripts.PutUsage("s-new", 12_000, A.At(9));
        _world.LaneSessions.Set("t1-api", "s-new");

        var row = (await Work())[0];

        Assert.Equal(("s-new", 12_000L, ContextVerdict.Ok), (row.Session!.Value.Value, row.Tokens, row.Verdict));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lane_with_no_session_or_no_transcript_or_no_usage_is_unknown_with_a_reason()
    {
        Claude("api", "s-api");
        _world.ClaudeTranscripts.Put("s-api", "{\"type\":\"user\"}\n", A.At(5));

        var rows = await Work();

        Assert.Equal([ContextVerdict.Unknown, ContextVerdict.Unknown], rows.Select(r => r.Verdict));
        Assert.Contains("no usage", rows[0].Reason, StringComparison.Ordinal);
        Assert.Contains("no session id", rows[1].Reason, StringComparison.Ordinal);

        _world.ClaudeTranscripts.Put("s-other", "x\n", A.At(5));
        Claude("api", "s-missing");
        Assert.Contains("no transcript", (await Work())[0].Reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_non_claude_lane_is_unknown_with_its_agent_as_the_reason()
    {
        _ = _world.PutLane("api", "codex");

        var row = (await Work())[0];

        Assert.Equal(ContextVerdict.Unknown, row.Verdict);
        Assert.Contains("codex", row.Reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_threshold_comes_from_the_team_config()
    {
        Claude("api", "s-api");
        _world.ClaudeTranscripts.PutUsage("s-api", 120_000, A.At(5));

        Assert.Equal(ContextVerdict.Recycle, (await Work(100_000))[0].Verdict);
        Assert.Equal(100_000, (await Work(100_000))[0].Threshold);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task One_unreadable_transcript_makes_only_that_lane_unknown()
    {
        Claude("api", "s-api");
        Claude("ui", "s-ui");
        _world.ClaudeTranscripts.FailFor("s-api");
        _world.ClaudeTranscripts.PutUsage("s-ui", 50_000, A.At(6));

        var rows = await Work();

        Assert.Equal([ContextVerdict.Unknown, ContextVerdict.Ok], rows.Select(r => r.Verdict));
        Assert.Contains("can't be read", rows[0].Reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_live_session_the_recorded_lane_sessions_id_is_measured()
    {
        Claude("api", "s-recorded");
        _world.ClaudeTranscripts.PutUsage("s-recorded", 77_000, A.At(5));

        var row = (await Work())[0];

        Assert.Equal(("s-recorded", 77_000L), (row.Session!.Value.Value, row.Tokens));
    }
}
