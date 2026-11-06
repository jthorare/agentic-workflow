using System.Globalization;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;
using Aw.Meta.Features.LaneInbox;
using Aw.Meta.Features.LaneSend;
using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.LaneSend;

/// <summary>
/// #2122: a lane is "taking part" only when one of its hooks ran (a hand-typed aw-meta call is not proof), health names why the hooks
/// did not, and a wake counts only when the lane's next hook answers it.
/// </summary>
public sealed class HookVerifiedDeliveryTests
{
    private const string CodexNeverRan = "hooks never ran: trust them with /hooks in codex (each install that changes a hook asks again)";

    private readonly MetaWorld _world = new();

    private LaneInboxService Inbox() => new(_world.Config, _world.LaneMail, _world.Beats, _world.Clock);

    private LaneSendService Service(IDelay delay) => new(_world.Config, _world.LaneFiles, _world.LaneMail, _world.Messages, new LaneBeats(_world.Values, delay), _world.Panes, _world.Clock);

    private async Task<LaneHealth> Health() =>
        Assert.Single((await _world.LaneHealth.Read((await _world.Config.Load(CancellationToken.None)).Value, CancellationToken.None)).Value);

    private async Task HookBeat(string lane, bool stopped, DateTimeOffset at) =>
        _ = await _world.Beats.Record(lane, stopped, at, CancellationToken.None);

    private async Task<LaneSendResponse> Send(IDelay delay)
    {
        var result = await Service(delay).Handle(new LaneSendRequest("api", "rebase please"), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    // A woken lane: idle on a hook Stop beat from before the clock, agent up in the pane.
    private async Task IdleLane(int clients = 0)
    {
        await _world.Ready();
        _ = _world.PutLane("api");
        await HookBeat("api", stopped: true, A.At(20));
        if (clients > 0)
        {
            _world.Panes.Put("t1-api", "codex", clients);
        }
    }

    private async Task<string?> WokeRecorded() => (await _world.Values.Get("lane.api.woke", CancellationToken.None)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task A_manual_lane_inbox_delivers_mail_but_is_not_hook_proof()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        _ = await _world.LaneMail.Send("api", "rebase please", A.At(5), CancellationToken.None);

        var response = await Inbox().Handle(new LaneInboxRequest(lane), CancellationToken.None);

        Assert.Equal("rebase please", Assert.Single(response.Value.Messages).Text);
        Assert.True(Assert.Single((await _world.LaneMail.Owed("api", CancellationToken.None)).Value).Delivered);
        Assert.Null((await _world.Beats.Last("api", CancellationToken.None)).Value);
        Assert.Equal(_world.Clock.GetUtcNow(), (await _world.Beats.LastCli("api", CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Health_says_hooks_not_verified_when_only_manual_runs_were_seen()
    {
        await _world.Ready();
        var lane = _world.PutLane("api");
        _ = await Inbox().Handle(new LaneInboxRequest(lane), CancellationToken.None);   // typed by hand, at the clock's 05:00:00Z

        var row = await Health();

        Assert.Equal(("hooks not verified: only manual aw-meta runs seen (last 05:00:00Z); " + CodexNeverRan, true), (row.State, row.Attention));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Health_names_not_own_agent_from_the_hook_trace()
    {
        await _world.Ready();
        _ = _world.PutLane("api");
        _world.LaneFiles.PutTrace("api", new LaneHookTrace(A.At(20), "meta-lane-gate", "UserPromptSubmit", "not-own-agent"));

        var row = await Health();

        Assert.Equal(("hooks ran but skipped the lane: the hook's process is not under the lane's agent (UserPromptSubmit at 04:20:00Z)", true), (row.State, row.Attention));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("codex", CodexNeverRan)]
    [InlineData("cursor", "hooks never ran: check ~/.cursor/hooks.json has the aw:meta-lane-gate entries (scripts/install-meta.sh)")]
    [InlineData("gemini", "hooks never ran: check the gemini hooks are installed")]
    public async Task Health_says_hooks_never_ran_when_there_is_no_trace(string agent, string expected)
    {
        await _world.Ready();
        _ = _world.PutLane("api", agent: agent);

        var row = await Health();

        Assert.Equal((expected, true), (row.State, row.Attention));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_trace_older_than_the_session_id_counts_as_none()
    {
        await _world.Ready();
        _ = _world.PutLane("api");   // the session id was recorded at minute 10
        _world.LaneFiles.PutTrace("api", new LaneHookTrace(A.At(5), "meta-lane-gate", "Stop", "not-own-agent"));

        Assert.Equal(CodexNeverRan, (await Health()).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wake_records_woke_only_after_a_new_hook_beat()
    {
        await IdleLane();
        var wokeBeforeTheBeat = "unset";
        var delay = Hooking(async (call, at) =>
        {
            if (call == 2)
            {
                wokeBeforeTheBeat = await WokeRecorded();
            }

            if (call == 3)
            {
                await HookBeat("api", stopped: false, at);
            }
        });

        var response = await Send(delay);

        Assert.Null(wokeBeforeTheBeat);
        Assert.True(response.Woken);
        Assert.NotNull(await WokeRecorded());
        Assert.Equal(0, _world.Panes.Enters);
        Assert.Equal(TimeSpan.FromMilliseconds(500), _world.Delay.Waits[0]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wake_presses_enter_again_when_no_hook_answers()
    {
        await IdleLane();
        var delay = Hooking(async (call, at) =>
        {
            if (call == 25)   // after the first 10 s window (20 polls of 500 ms) and the second Enter
            {
                await HookBeat("api", stopped: false, at);
            }
        });

        var response = await Send(delay);

        Assert.Equal(1, _world.Panes.Enters);
        Assert.True(response.Woken);
        Assert.NotNull(await WokeRecorded());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wake_unconfirmed_after_both_waits_is_not_woken_and_says_why()
    {
        await IdleLane();

        var response = await Send(Hooking((_, _) => ValueTask.CompletedTask));

        Assert.False(response.Woken);
        Assert.Equal("typed the wake line but no hook answered in 20 s: the line may sit unsent in the pane (aw-meta lane-send again, or press Enter in it)", response.WakeSkipped);
        Assert.Null(response.Attention);
        Assert.Equal(1, _world.Panes.Enters);
        Assert.Null(await WokeRecorded());
        Assert.Equal(TimeSpan.FromSeconds(20), _world.Delay.Waits.Aggregate(TimeSpan.Zero, static (sum, wait) => sum + wait));
        Assert.True((await _world.Beats.IsIdle("api", CancellationToken.None)).Value);   // so the next send tries again
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_attached_viewer_does_not_block_the_wake()
    {
        await IdleLane(clients: 1);
        var delay = Hooking(async (call, at) =>
        {
            if (call == 1)
            {
                await HookBeat("api", stopped: false, at);
            }
        });

        var response = await Send(delay);

        Assert.True(response.Woken);
        Assert.Null(response.WakeSkipped);
        _ = Assert.Single(_world.Panes.Woken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Wake_pauses_between_the_line_and_enter()
    {
        var runner = new InMemoryCommandRunner().When("send-keys");
        var linesWhenWaiting = new List<int>();
        var delay = Hooking((_, _) =>
        {
            linesWhenWaiting.Add(runner.Lines.Count);
            return ValueTask.CompletedTask;
        });
        var pane = new TmuxLanePane(runner, "tmux", "t1", _world.Config, delay);

        var result = await pane.Wake("t1-api", "hello", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([TimeSpan.FromMilliseconds(300)], _world.Delay.Waits);
        Assert.Equal([1], linesWhenWaiting);   // the line was typed, Enter not yet
        Assert.Equal(["tmux -L t1 send-keys -t =t1-api: -l hello", "tmux -L t1 send-keys -t =t1-api: Enter"], runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Health_flags_mail_undelivered_for_five_minutes()
    {
        await _world.Ready();
        _ = _world.PutLane("api");
        await HookBeat("api", stopped: false, A.At(55));
        _world.LaneFiles.PutTrace("api", new LaneHookTrace(A.At(55), "meta-lane-gate", "UserPromptSubmit", "ran"));
        _ = await _world.LaneMail.Send("api", "rebase please", A.At(50), CancellationToken.None);   // the clock is at minute 60

        var row = await Health();

        Assert.Equal(("mail not delivered for 10 min: the lane's last hook was UserPromptSubmit at 04:55:00Z; lane-send wakes an idle lane", true), (row.State, row.Attention));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Old_two_part_beats_still_read_as_hook_beats()
    {
        await _world.Ready();
        _ = _world.PutLane("api");
        _ = await _world.Values.Set("lane.api.beat", A.At(15).ToString("O", CultureInfo.InvariantCulture) + "|stop", CancellationToken.None);

        var beat = (await _world.Beats.Last("api", CancellationToken.None)).Value;

        Assert.Equal((A.At(15), true), (beat!.At, beat.Stopped));
        Assert.Equal(("taking part", false), ((await Health()).State, (await Health()).Attention));
        Assert.True((await _world.Beats.IsIdle("api", CancellationToken.None)).Value);
    }

    private HookingDelay Hooking(Func<int, DateTimeOffset, ValueTask> afterWait) => new(_world.Delay, _world.Clock, afterWait);

    // A delay that runs a hook after each wait, as the lane's own hook would while the meta waits.
    private sealed class HookingDelay(InMemoryDelay inner, ManualClock clock, Func<int, DateTimeOffset, ValueTask> afterWait) : IDelay
    {
        private int _calls;

        public async ValueTask Wait(TimeSpan duration, CancellationToken cancellationToken)
        {
            await inner.Wait(duration, cancellationToken);
            _calls++;
            await afterWait(_calls, clock.GetUtcNow());
        }
    }
}
