using System.Text.Json.Nodes;
using Aw.Sindri.Features.Line;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>The line's phase board (line spec §8.2, §8.3, LN-WF1): the fold of phase events and the owner's edits, and the pick of the next phase.</summary>
public sealed class PhaseBoardTests
{
    private static PhaseBoard Plan(params (string Phase, string[] Deps)[] phases) => PhaseBoard.Of(new JsonArray([.. phases.Select(p => (JsonNode?)new JsonObject
    {
        ["phase"] = p.Phase,
        ["id"] = p.Phase,
        ["hash"] = "sha256:" + p.Phase,
        ["dir"] = "phase-packets/" + p.Phase,
        ["deps"] = new JsonArray([.. p.Deps.Select(static d => (JsonNode?)d)]),
    })]));

    private static JsonObject Event(string phase, int attempt, string outcome, string? headBranch = null, string? ended = null, string? packet = null) => new()
    {
        ["phase"] = phase,
        ["attempt"] = attempt,
        ["outcome"] = outcome,
        ["headBranch"] = headBranch,
        ["ended"] = ended,
        ["packet"] = packet,
    };

    private static string States(PhaseBoard board) => string.Join(' ', board.Rows.Select(r => $"{r.Phase}={PhaseRow.Name(r.State)}"));

    private static PhaseBoard Started(PhaseBoard board, params string[] phases)
    {
        foreach (var phase in phases)
        {
            board.Fold(Event(phase, 1, "started"));
        }

        return board;
    }

    [Fact, Trait("Lane", "unit")]
    public void The_board_of_a_plan_lists_every_phase_waiting_in_plan_order()
    {
        var board = Plan(("a", []), ("b", ["a"]), ("c", []));

        Assert.Equal("a=waiting b=waiting c=waiting", States(board));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_board_reads_back_as_it_was_written()
    {
        var board = Started(Plan(("a", []), ("b", ["a"])), "a");

        var again = PhaseBoard.Read(board.ToJson());

        Assert.NotNull(again);
        Assert.Equal(board.ToJson().ToJsonString(), again.ToJson().ToJsonString());
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_phases_with_no_dependency_start_one_after_the_other_up_to_the_parallel_cap()
    {
        var board = Plan(("a", []), ("b", []), ("c", []));

        var first = board.Decide(parallel: 2, maxOpenPrs: 4);
        PhaseBoard.Start(first.Next!);
        var second = board.Decide(parallel: 2, maxOpenPrs: 4);
        PhaseBoard.Start(second.Next!);
        var third = board.Decide(parallel: 2, maxOpenPrs: 4);

        Assert.Equal(("start", "a"), (first.Outcome, first.Next?.Row.Phase));
        Assert.Equal(("start", "b"), (second.Outcome, second.Next?.Row.Phase));
        Assert.Equal(("full", null), (third.Outcome, third.Next?.Row.Phase));
        Assert.Equal("a=running b=running c=waiting", States(board));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_phase_whose_dependency_has_shipped_starts_stacked_on_its_head_branch()
    {
        var board = Started(Plan(("a", []), ("b", ["a"])), "a");
        board.Fold(Event("a", 1, "shipped", headBranch: "sindri/a"));

        var decision = board.Decide(parallel: 2, maxOpenPrs: 4);

        Assert.Equal("start", decision.Outcome);
        Assert.Equal("""{"rule":"dependency-head","ref":"sindri/a"}""", decision.Next?.ToJson()["base"]?.ToJsonString());
        Assert.Equal("a", decision.Next?.ToJson()["stackParent"]?.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_phase_whose_dependency_still_runs_waits_for_it()
    {
        var board = Started(Plan(("a", []), ("b", ["a"])), "a");

        var decision = board.Decide(parallel: 2, maxOpenPrs: 4);

        Assert.Equal(("full", null), (decision.Outcome, decision.Next?.Row.Phase));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_phase_whose_dependencies_all_merged_starts_on_the_default_branch()
    {
        var board = Started(Plan(("a", []), ("b", ["a"])), "a");
        board.Fold(Event("a", 1, "shipped", headBranch: "sindri/a"));
        board.Fold(Event("a", 1, "merged"));

        var decision = board.Decide(parallel: 2, maxOpenPrs: 4);

        Assert.Equal("""{"rule":"default-branch","ref":""}""", decision.Next?.ToJson()["base"]?.ToJsonString());
        Assert.Null(decision.Next?.ToJson()["stackParent"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_open_dependencies_hold_a_phase_back_even_when_both_shipped()
    {
        var board = Started(Plan(("a", []), ("b", []), ("c", ["a", "b"])), "a", "b");
        board.Fold(Event("a", 1, "shipped", headBranch: "sindri/a"));
        board.Fold(Event("b", 1, "shipped", headBranch: "sindri/b"));

        var decision = board.Decide(parallel: 3, maxOpenPrs: 4);

        Assert.Equal("full", decision.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void Shipped_phases_hold_no_running_slot_but_count_against_the_open_pr_cap()
    {
        var board = Started(Plan(("a", []), ("b", []), ("c", [])), "a");
        board.Fold(Event("a", 1, "shipped", headBranch: "sindri/a"));

        var room = board.Decide(parallel: 1, maxOpenPrs: 2);
        var capped = board.Decide(parallel: 1, maxOpenPrs: 1);

        Assert.Equal(("start", "b"), (room.Outcome, room.Next?.Row.Phase));
        Assert.Equal("full", capped.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_phase_that_ended_without_merging_blocks_the_line_for_the_owner()
    {
        var board = Started(Plan(("a", []), ("b", []), ("c", ["a"])), "a");
        board.Fold(Event("a", 1, "ended", ended: "unresolved"));

        var decision = board.Decide(parallel: 2, maxOpenPrs: 4);

        Assert.Equal(("blocked", null), (decision.Outcome, decision.Next?.Row.Phase));
        Assert.Equal("a=failed b=waiting c=waiting", States(board));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_phase_that_ended_resolved_is_merged()
    {
        var board = Started(Plan(("a", [])), "a");
        board.Fold(Event("a", 1, "ended", ended: "resolved"));

        Assert.Equal("a=merged", States(board));
        Assert.Equal("done", board.Decide(parallel: 2, maxOpenPrs: 4).Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void Retry_puts_a_failed_phase_back_for_its_next_attempt_from_the_newest_packet()
    {
        var board = Started(Plan(("a", [])), "a");
        board.Fold(Event("a", 1, "ended", ended: "unresolved", packet: "sha256:amended"));

        board.Edit(new JsonObject { ["edit"] = "retry", ["phase"] = "a" });
        var decision = board.Decide(parallel: 2, maxOpenPrs: 4);

        Assert.Equal("start", decision.Outcome);
        Assert.Equal(2, decision.Next?.Attempt);
        Assert.Equal("sha256:amended", decision.Next?.ToJson()["packet"]?["hash"]?.GetValue<string>());
    }

    [Fact, Trait("Lane", "unit")]
    public void Skip_skips_the_phase_and_everything_that_depends_on_it()
    {
        var board = Started(Plan(("a", []), ("b", ["a"]), ("c", ["b"]), ("d", [])), "a");
        board.Fold(Event("a", 1, "ended", ended: "failed"));

        board.Edit(new JsonObject { ["edit"] = "skip", ["phase"] = "a" });

        Assert.Equal("a=skipped b=skipped c=skipped d=waiting", States(board));
        Assert.Equal("start", board.Decide(parallel: 2, maxOpenPrs: 4).Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_board_of_merged_and_skipped_phases_is_done()
    {
        var board = Started(Plan(("a", []), ("b", [])), "a");
        board.Fold(Event("a", 1, "merged"));
        board.Edit(new JsonObject { ["edit"] = "skip", ["phase"] = "b" });

        Assert.Equal("done", board.Decide(parallel: 2, maxOpenPrs: 4).Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_parked_phase_stays_running_and_a_resume_clears_the_park()
    {
        var board = Started(Plan(("a", [])), "a");

        board.Fold(new JsonObject { ["phase"] = "a", ["attempt"] = 1, ["outcome"] = "parked", ["parkReason"] = "needs an answer" });
        var parked = (board.Rows[0].State, board.Rows[0].Parked, board.Rows[0].ParkReason);
        board.Fold(Event("a", 1, "resumed"));

        Assert.Equal((PhaseState.Running, true, "needs an answer"), parked);
        Assert.Equal((PhaseState.Running, false, null), (board.Rows[0].State, board.Rows[0].Parked, board.Rows[0].ParkReason));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_late_or_stale_event_changes_nothing()
    {
        var board = Started(Plan(("a", [])), "a");
        board.Fold(Event("a", 1, "shipped", headBranch: "sindri/a"));
        var before = board.ToJson().ToJsonString();

        board.Fold(Event("a", 1, "started"));
        board.Fold(Event("zzz", 1, "merged"));
        board.Fold(new JsonObject());
        board.Fold(phaseEvent: null);

        Assert.Equal(before, board.ToJson().ToJsonString());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_event_for_an_older_attempt_is_dropped_after_a_retry()
    {
        var board = Started(Plan(("a", [])), "a");
        board.Fold(Event("a", 1, "ended", ended: "failed"));
        board.Edit(new JsonObject { ["edit"] = "retry", ["phase"] = "a" });
        PhaseBoard.Start(board.Decide(parallel: 2, maxOpenPrs: 4).Next!);

        board.Fold(Event("a", 1, "ended", ended: "failed"));

        Assert.Equal(("a=running", 2), (States(board), board.Rows[0].Attempt));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_phase_depending_on_one_the_plan_lacks_never_starts()
    {
        var board = Plan(("a", ["ghost"]));

        Assert.Equal("blocked", board.Decide(parallel: 2, maxOpenPrs: 4).Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_report_event_is_read_by_its_status_and_the_attempt_of_its_instance()
    {
        var board = Started(Plan(("a", [])), "a");
        board.Fold(new JsonObject { ["line"] = "T-1/line/1", ["phase"] = "a", ["status"] = "shipped", ["instance"] = "T-1/line/1/phase.a.1", ["at"] = "2026-10-22T10:00:00Z", ["headBranch"] = "sindri/a" });

        Assert.Equal(("a=shipped", "sindri/a"), (States(board), board.Rows[0].HeadBranch));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_report_fails_the_phase_and_blocks_the_line()
    {
        var board = Started(Plan(("a", []), ("b", ["a"])), "a");
        board.Fold(new JsonObject { ["phase"] = "a", ["status"] = "failed", ["instance"] = "T-1/line/1/phase.a.1" });

        Assert.Equal(("a=failed b=waiting", "blocked"), (States(board), board.Decide(parallel: 2, maxOpenPrs: 4).Outcome));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_report_of_an_older_attempt_is_dropped_after_a_retry()
    {
        var board = Started(Plan(("a", [])), "a");
        board.Fold(Event("a", 1, "failed"));
        board.Edit(new JsonObject { ["edit"] = "retry", ["phase"] = "a" });
        PhaseBoard.Start(board.Decide(parallel: 2, maxOpenPrs: 4).Next!);

        board.Fold(new JsonObject { ["phase"] = "a", ["status"] = "failed", ["instance"] = "T-1/line/1/phase.a.1" });

        Assert.Equal("a=running", States(board));
    }
}
