using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Agents;

/// <summary>The Agents page's reasons (Agents page spec §3.2): one fixture per reason, with the class, the chip word and the words on the row.</summary>
public sealed class AgentReasonsTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 12, 9, 10, 0, TimeSpan.Zero);

    private static AgentReasonInputs Lane(string name = "qa", int idleMinutes = 3) =>
        new(_now, name, _now.AddMinutes(-idleMinutes)) { Lane = name };

    private static AgentReason Only(AgentReasonInputs inputs) => Assert.Single(AgentReasons.Evaluate(inputs));

    [Fact, Trait("Lane", "unit")]
    public void A_row_with_nothing_owed_has_no_reason()
    {
        Assert.Empty(AgentReasons.Evaluate(Lane("api", idleMinutes: 31) with { State = SessionState.Idle, ContextTokens = 61_000 }));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_permission_prompt_is_class_1_and_names_the_tool()
    {
        var asking = new SessionAsking(AskingReason.Approval, "Bash", _now.AddSeconds(-5));

        var reason = Only(Lane() with { State = SessionState.Asking, Asking = asking });

        Assert.Equal(new AgentReason(1, AgentReasonKind.PermissionPrompt, "asking", "qa: asking to run Bash", asking.Since, AgentStateSource.Hook), reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_permission_prompt_with_no_tool_says_it_asks_for_approval()
    {
        var reason = Only(Lane() with { State = SessionState.Asking, Asking = new SessionAsking(AskingReason.Approval, Tool: null, _now) });

        Assert.Equal("qa: asking for approval", reason.Text);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(AskingReason.Input, AgentReasonKind.OwnerQuestion, "question", "ux: asking you a question")]
    [InlineData(AskingReason.Auth, AgentReasonKind.Login, "login", "ux: needs a login")]
    public void A_question_and_a_login_are_class_1(AskingReason asked, AgentReasonKind kind, string word, string text)
    {
        var reason = Only(Lane("ux") with { State = SessionState.Asking, Asking = new SessionAsking(asked, Tool: null, _now) });

        Assert.Equal((1, kind, word, text), (reason.Class, reason.Kind, reason.Word, reason.Text));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_question_from_the_engine_keeps_the_engine_as_its_source()
    {
        var reason = Only(Lane("ux") with { State = SessionState.Asking, StateSource = AgentStateSource.Engine, Asking = new SessionAsking(AskingReason.Input, Tool: null, _now) });

        Assert.Equal(AgentStateSource.Engine, reason.Source);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_usage_limit_is_class_2_and_words_the_reset_time()
    {
        var since = _now.AddMinutes(-20);
        var resets = new DateTimeOffset(2026, 11, 12, 14, 0, 0, TimeSpan.Zero);

        var known = Only(Lane("tools") with { UsageLimited = true, UsageResetsAt = resets, UsageSince = since });
        var unknown = Only(Lane("tools") with { UsageLimited = true });

        Assert.Equal(new AgentReason(2, AgentReasonKind.UsageLimit, "back at 14:00", "tools: hit its usage limit, resets 14:00", since, AgentStateSource.AwMeta), known);
        Assert.Equal(("limited", "tools: hit its usage limit, reset time unknown", _now), (unknown.Word, unknown.Text, unknown.Since));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_with_no_live_session_is_class_3()
    {
        var reason = Only(Lane("design") with { HasLiveSession = false });

        Assert.Equal((3, AgentReasonKind.LaneNoSession, "no session", "design: no live session"), (reason.Class, reason.Kind, reason.Word, reason.Text));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_the_owner_stopped_and_a_session_with_no_lane_are_not_a_lane_with_no_session()
    {
        Assert.Empty(AgentReasons.Evaluate(Lane("design") with { HasLiveSession = false, StoppedFromPage = true }));
        Assert.Empty(AgentReasons.Evaluate(Lane("judge-cli") with { Lane = null, HasLiveSession = false }));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_at_the_recycle_mark_is_class_4_and_words_its_context_in_thousands()
    {
        var since = new DateTimeOffset(2026, 11, 12, 8, 50, 0, TimeSpan.Zero);

        var reason = Only(Lane("ideas") with { AtRecycleMark = true, ContextTokens = 210_000, RecycleSince = since });

        Assert.Equal(new AgentReason(4, AgentReasonKind.RecycleMark, "recycle", "ideas: at 210k, recycle from a digest", since, AgentStateSource.AwMeta), reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_meta_lane_with_an_open_recycle_obligation_is_at_the_mark_even_without_a_size()
    {
        var meta = Only(Lane("meta") with { IsMetaLane = true, RecycleSelfOpen = true });
        var other = AgentReasons.Evaluate(Lane("qa") with { RecycleSelfOpen = true });

        Assert.Equal((AgentReasonKind.RecycleMark, "meta: recycle from a digest"), (meta.Kind, meta.Text));
        Assert.Empty(other);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_idle_ten_minutes_with_a_PR_that_failed_its_gate_is_class_5()
    {
        var inputs = Lane("qa", idleMinutes: 14) with { State = SessionState.Idle, QueuedPrs = [new AgentQueuedPr(3067, AgentGate.Fail, InGithubQueue: false, _now.AddMinutes(-20))] };

        var reason = Only(inputs);

        Assert.Equal(new AgentReason(5, AgentReasonKind.IdleOwed, "work owed", "qa: idle 14 min, PR #3067 failed its gate", _now.AddMinutes(-4), AgentStateSource.AwMeta), reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_PR_with_a_conflict_is_owed_work_too()
    {
        var inputs = Lane("qa", idleMinutes: 12) with { State = SessionState.Idle, QueuedPrs = [new AgentQueuedPr(3067, AgentGate.Conflict, InGithubQueue: false, _now.AddMinutes(-20))] };

        Assert.Equal("qa: idle 12 min, PR #3067 has a conflict", Only(inputs).Text);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_meta_lane_owes_its_open_obligations_and_counts_them()
    {
        var one = Only(Lane("meta", idleMinutes: 15) with { State = SessionState.Idle, IsMetaLane = true, OpenObligations = 1 });
        var two = Only(Lane("meta", idleMinutes: 15) with { State = SessionState.Idle, IsMetaLane = true, OpenObligations = 2 });

        Assert.Equal(("meta: idle 15 min, 1 open obligation", "meta: idle 15 min, 2 open obligations"), (one.Text, two.Text));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_that_is_not_Claude_owes_mail_that_was_not_delivered()
    {
        var late = Only(Lane("codex-lane", idleMinutes: 11) with { State = SessionState.Idle, IsClaudeLane = false, MailUndeliveredMinutes = 12 });
        var claude = AgentReasons.Evaluate(Lane("qa", idleMinutes: 11) with { State = SessionState.Idle, MailUndeliveredMinutes = 12 });

        Assert.Equal("codex-lane: idle 11 min, mail not delivered for 12 min", late.Text);
        Assert.Empty(claude);
    }

    [Fact, Trait("Lane", "unit")]
    public void Obligations_count_for_the_meta_lane_only()
    {
        Assert.Empty(AgentReasons.Evaluate(Lane("qa", idleMinutes: 30) with { State = SessionState.Idle, OpenObligations = 3 }));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lane_idle_nine_minutes_does_not_qualify_yet_and_the_threshold_comes_from_the_inputs()
    {
        var owed = new AgentQueuedPr(3067, AgentGate.Fail, InGithubQueue: false, _now.AddMinutes(-20));
        var nine = Lane("qa", idleMinutes: 9) with { State = SessionState.Idle, QueuedPrs = [owed] };

        Assert.Empty(AgentReasons.Evaluate(nine));
        Assert.Equal(AgentReasonKind.IdleOwed, Only(nine with { IdleOwedMinutes = 5 }).Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_estimated_idle_state_and_a_working_lane_are_never_idle_with_work_owed()
    {
        var owed = new AgentQueuedPr(3067, AgentGate.Fail, InGithubQueue: false, _now.AddMinutes(-20));

        Assert.Empty(AgentReasons.Evaluate(Lane("qa", idleMinutes: 30) with { State = SessionState.Idle, Estimated = true, QueuedPrs = [owed] }));
        Assert.Empty(AgentReasons.Evaluate(Lane("qa", idleMinutes: 30) with { State = SessionState.Running, QueuedPrs = [owed] }));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_heavy_waiter_over_thirty_minutes_is_class_6()
    {
        var since = _now.AddMinutes(-39);

        var reason = Only(Lane("tools") with { HeavyWait = new AgentHeavyWait(since, Held: 3, Cap: 3) });

        Assert.Equal(new AgentReason(6, AgentReasonKind.SlotWait, "waiting for a slot", "tools: waiting 39 min for a heavy slot (3 of 3 held)", since, AgentStateSource.AwMeta), reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_heavy_waiter_under_the_limit_has_no_reason_and_the_limit_comes_from_the_inputs()
    {
        var waiting = Lane("tools") with { HeavyWait = new AgentHeavyWait(_now.AddMinutes(-29), Held: 3, Cap: 3) };

        Assert.Empty(AgentReasons.Evaluate(waiting));
        Assert.Equal(AgentReasonKind.SlotWait, Only(waiting with { SlotWaitMinutes = 20 }).Kind);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(AgentGate.Pass)]
    [InlineData(AgentGate.None)]
    public void A_PR_ready_in_the_merge_queue_is_class_7(AgentGate gate)
    {
        var ready = _now.AddMinutes(-12);

        var reason = Only(Lane("ideas") with { QueuedPrs = [new AgentQueuedPr(3078, gate, InGithubQueue: false, ready)] });

        Assert.Equal(new AgentReason(7, AgentReasonKind.PrReady, "PR ready", "ideas: PR #3078 ready in the merge queue", ready, AgentStateSource.AwMeta), reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_PR_that_failed_or_that_Meta_already_queued_is_not_ready_for_the_owner()
    {
        var failed = new AgentQueuedPr(1, AgentGate.Fail, InGithubQueue: false, _now);
        var conflict = new AgentQueuedPr(2, AgentGate.Conflict, InGithubQueue: false, _now);
        var merging = new AgentQueuedPr(3, AgentGate.Pass, InGithubQueue: true, _now);

        Assert.Empty(AgentReasons.Evaluate(Lane("ideas") with { QueuedPrs = [failed, conflict, merging] }));
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_ready_PRs_are_two_reasons_oldest_first()
    {
        var newer = new AgentQueuedPr(3079, AgentGate.Pass, InGithubQueue: false, _now.AddMinutes(-2));
        var older = new AgentQueuedPr(3078, AgentGate.Pass, InGithubQueue: false, _now.AddMinutes(-9));

        var reasons = AgentReasons.Evaluate(Lane("ideas") with { QueuedPrs = [newer, older] });

        Assert.Equal(["ideas: PR #3078 ready in the merge queue", "ideas: PR #3079 ready in the merge queue"], reasons.Select(static r => r.Text));
    }

    [Fact, Trait("Lane", "unit")]
    public void Reasons_come_lowest_class_first_so_the_first_is_the_primary()
    {
        var asking = new SessionAsking(AskingReason.Approval, "Bash", _now);
        var inputs = Lane("qa") with
        {
            State = SessionState.Asking,
            Asking = asking,
            AtRecycleMark = true,
            ContextTokens = 230_000,
            QueuedPrs = [new AgentQueuedPr(3067, AgentGate.Pass, InGithubQueue: false, _now.AddMinutes(-5))],
        };

        Assert.Equal([1, 4, 7], AgentReasons.Evaluate(inputs).Select(static r => r.Class));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_inputs_give_the_same_reasons()
    {
        var inputs = Lane("ideas") with { AtRecycleMark = true, ContextTokens = 210_000, QueuedPrs = [new AgentQueuedPr(3078, AgentGate.Pass, InGithubQueue: false, _now)] };

        Assert.Equal(AgentReasons.Evaluate(inputs), AgentReasons.Evaluate(inputs));
    }
}
