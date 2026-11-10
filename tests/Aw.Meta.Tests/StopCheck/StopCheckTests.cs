using Aw.Meta.Core;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.StopCheck;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.StopCheck;

public sealed class StopCheckTests
{
    private readonly MetaWorld _world = new();

    private StopCheckService Service() => new(_world.Session, _world.Sweep, _world.Guard, _world.Clock);

    private async Task<StopCheckResponse> Check(string session = MetaWorld.MetaSessionId, bool hookActive = false)
    {
        var result = await Service().Handle(new StopCheckRequest(SessionId.From(session), MetaWorld.Transcript, hookActive, MaxBlocks: 3), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Any_other_session_may_stop_and_nothing_is_read()
    {
        await _world.Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "please rebase"));

        var response = await Check(session: "s-other");

        Assert.Equal((false, false), (response.IsMeta, response.Blocked));
        Assert.Empty((await _world.Messages.Messages(CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task After_clear_the_new_session_id_under_the_registered_name_is_still_the_meta_and_is_registered()
    {
        await _world.Ready();
        _world.Sessions.Add(A.Live("t1-meta", "s-after-clear"));
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "please rebase"));

        var response = await Check(session: "s-after-clear");

        Assert.Equal((true, true), (response.IsMeta, response.Blocked));
        Assert.Equal("s-after-clear", (await _world.Messages.Role(CancellationToken.None)).Value!.Session.Value);
        Assert.True((await Check(session: "s-after-clear")).IsMeta);
        Assert.False((await Check()).IsMeta);   // the old id no longer is
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("t1-api", "s-new")]
    [InlineData("", "s-new")]
    public async Task A_live_session_with_another_name_is_not_the_meta(string name, string session)
    {
        await _world.Ready();
        _world.Sessions.Add(A.Live(name, session));
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "please rebase"));

        var response = await Check(session: session);

        Assert.False(response.IsMeta);
        Assert.Equal(MetaWorld.MetaSessionId, (await _world.Messages.Role(CancellationToken.None)).Value!.Session.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_that_is_not_live_is_not_the_meta_even_with_the_registered_name_elsewhere()
    {
        await _world.Ready();
        _world.Sessions.Add(A.Live("t1-meta", "s-someone-else"));

        Assert.False((await Check(session: "s-not-listed")).IsMeta);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_turn_restarts_the_block_count()
    {
        await _world.Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "stuck"));
        var turns = new List<bool[]>();
        for (var turn = 0; turn < 2; turn++)
        {
            var blocks = new List<bool> { (await Check(hookActive: false)).Blocked };
            for (var i = 0; i < 4; i++)
            {
                blocks.Add((await Check(hookActive: true)).Blocked);
            }

            turns.Add([.. blocks]);
        }

        Assert.Equal([true, true, true, true, false], turns[0]);
        Assert.Equal(turns[0], turns[1]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_meta_with_an_unanswered_peer_message_is_blocked_with_the_exact_next_command()
    {
        await _world.Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "please rebase"));

        var response = await Check();

        var reply = Assert.Single(response.Open, o => o.Kind == ObligationKind.Reply);
        Assert.True(response.Blocked);
        Assert.Equal((ObligationKind.Reply, "m1"), (reply.Kind, reply.Key));
        Assert.Contains("aw-meta inbox-dismiss m1", reply.NextCommand, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Answering_the_peer_releases_the_stop()
    {
        await _world.Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "please rebase"));
        _ = await Check();
        _ = await _world.Messages.RecordOutbound(A.Sent("t1-api", "done", 61), CancellationToken.None);

        var response = await Check();

        Assert.False(response.Blocked);
        Assert.Empty(response.Open);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dismissed_message_does_not_block_and_a_deferred_one_blocks_again_when_due()
    {
        await _world.Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "a") + MetaWorld.PeerLine("m2", "t1-ui", "b"));
        _ = await Check();
        _ = await _world.Messages.Resolve(MessageId.From("m1"), new(Core.Messages.MessageResolutionKind.Dismissed, "fyi"), CancellationToken.None);
        _ = await _world.Messages.Resolve(MessageId.From("m2"), new(Core.Messages.MessageResolutionKind.Deferred, "later", _world.Clock.GetUtcNow().AddMinutes(30)), CancellationToken.None);

        var snoozed = await Check();
        _world.Clock.Advance(TimeSpan.FromMinutes(31));
        var due = await Check();

        Assert.False(snoozed.Blocked);
        Assert.Equal(["m2", "watch"], due.Open.Select(o => o.Key), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_old_heartbeat_asks_to_rearm_the_watch_but_no_heartbeat_does_not()
    {
        await _world.Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, string.Empty);
        var none = await Check();
        _ = await _world.Values.Set(KvKeys.WatchHeartbeat, A.At(50).ToString("O", System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None);

        var stale = await Check();

        Assert.False(none.Blocked);
        Assert.Equal([ObligationKind.RearmWatch], stale.Open.Select(o => o.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_same_open_set_blocks_three_times_then_a_continuation_is_let_through_with_a_warning_and_one_incident()
    {
        await _world.Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "stuck"));

        var blocks = new List<bool>();
        for (var i = 0; i < 3; i++)
        {
            blocks.Add((await Check(hookActive: true)).Blocked);
        }

        var allowed = await Check(hookActive: true);
        var again = await Check(hookActive: true);

        Assert.Equal([true, true, true], blocks);
        Assert.False(allowed.Blocked);
        Assert.StartsWith("aw-meta: stop allowed after 3 identical blocks", allowed.Warning, StringComparison.Ordinal);
        Assert.NotNull(again.Warning);
        _ = Assert.Single(_world.Log.Lines, l => l.StartsWith("- INCIDENT ", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_hook_active_it_never_lets_the_stop_through_and_a_new_set_restarts_the_count()
    {
        await _world.Ready();
        _world.Transcripts.Write(MetaWorld.Transcript, MetaWorld.PeerLine("m1", "t1-api", "stuck"));
        for (var i = 0; i < 5; i++)
        {
            Assert.True((await Check(hookActive: false)).Blocked);
        }

        _world.Transcripts.Append(MetaWorld.Transcript, MetaWorld.PeerLine("m2", "t1-ui", "another"));

        Assert.True((await Check(hookActive: true)).Blocked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_team_config_is_AW_META_002()
    {
        _ = await _world.Messages.SetRole(new(SessionId.From(MetaWorld.MetaSessionId), "m"), CancellationToken.None);

        var result = await Service().Handle(new StopCheckRequest(SessionId.From(MetaWorld.MetaSessionId), MetaWorld.Transcript, HookActive: false, MaxBlocks: 3), CancellationToken.None);

        Assert.Equal(MetaErrors.NoTeamConfig.Code, result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("", 3)]
    [InlineData("/t.jsonl", 0)]
    public void A_blank_transcript_or_a_non_positive_limit_is_refused(string transcript, int max)
    {
        var result = new StopCheckValidator().Validate(new StopCheckRequest(SessionId.From("s"), transcript, HookActive: false, max));

        Assert.Equal(MetaErrors.Invalid.Code, result.Error.Code);
    }
}
