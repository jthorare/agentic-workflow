using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Stats;
using static Aw.Sindri.Tests.GetRecipeStats.StatRows;

namespace Aw.Sindri.Tests.GetRecipeStats;

/// <summary>The recipe stats counted from workflow ledger rows (API spec §3.8, E-24).</summary>
[Lane(TestLane.Unit)]
public sealed class RecipeStatsCounterTests
{
    private static readonly RecipeId _item = RecipeId.From("workflow.item");

    private static RecipeStats Count(IReadOnlyList<StatEvent> rows, StatsWindow? window = null, string? hash = null) =>
        RecipeStatsCounter.Count(rows, _item, hash, window ?? StatsWindow.ThirtyDays, Now);

    [Fact]
    public void Runs_are_counted_by_how_they_ended_and_a_run_that_started_before_the_window_is_not_counted()
    {
        var rows = new List<StatEvent>
        {
            Start("T-1/item/1", Ago(10)), End("T-1/item/1", Ago(9), "produce", "done", "resolved"),
            Start("T-2/item/1", Ago(8)), End("T-2/item/1", Ago(7), "produce", "failed", "failed"),
            Start("T-3/item/1", Ago(6)), End("T-3/item/1", Ago(5), "produce", "cancelled", "cancelled"),
            Start("T-4/item/1", Ago(4)),
            Start("T-5/item/1", Ago(40)), End("T-5/item/1", Ago(39), "produce", "done", "resolved"),
            Start("T-6/other/1", Ago(3), recipe: "workflow.other"),
        };

        var stats = Count(rows);

        Assert.Equal((4, 1, 1, 1), (stats.Runs, stats.Done, stats.Failed, stats.Cancelled));
        Assert.Equal(("workflow.item", "30d", false, null), (stats.Recipe, stats.Window, stats.ChildOnly, stats.Hash));
        Assert.Equal(Now, stats.AsOf);
    }

    [Fact]
    public void A_shorter_window_counts_fewer_runs()
    {
        var rows = new List<StatEvent>
        {
            Start("T-1/item/1", Ago(10)), End("T-1/item/1", Ago(9), "produce", "done", "resolved"),
            Start("T-2/item/1", Ago(2)), End("T-2/item/1", Ago(1), "produce", "done", "resolved"),
        };

        Assert.Equal((2, 1), (Count(rows).Runs, Count(rows, StatsWindow.SevenDays).Runs));
    }

    [Fact]
    public void A_hash_scopes_the_stats_to_the_runs_under_it_by_prefix()
    {
        var rows = new List<StatEvent>
        {
            Start("T-1/item/1", Ago(3)), Start("T-2/item/1", Ago(3), hash: OtherHash),
        };

        Assert.Equal((1, 1, 2), (Count(rows, hash: "sha256:11111111").Runs, Count(rows, hash: OtherHash).Runs, Count(rows).Runs));
        Assert.Equal("sha256:11111111", Count(rows, hash: "sha256:11111111").Hash);
    }

    [Fact]
    public void A_run_with_a_park_answered_an_escalation_decided_and_a_takeover_counts_once_in_the_human_rate()
    {
        var a = "T-1/item/1";
        var rows = new List<StatEvent>
        {
            Start(a, Ago(5)),
            Park(a, Ago(5, -10), "produce", ParkReason.NoProgress), Resume(a, Ago(5, -20), "produce"),
            Park(a, Ago(5, -30), "produce", ParkReason.Budget), Resume(a, Ago(5, -40), "produce"),
            Escalate(a, Ago(5, -50), "verify"), Resume(a, Ago(5, -60), "verify"),
            TakeOver(a, Ago(5, -70), "verify"), Resume(a, Ago(5, -80), "verify", answered: false),
            End(a, Ago(4), "verify", "done", "resolved"),
            Start("T-2/item/1", Ago(3)), End("T-2/item/1", Ago(2), "produce", "done", "resolved"),
        };

        var stats = Count(rows);

        Assert.Equal(2, stats.Runs);
        Assert.Equal(0.5, stats.HumanRate);
        Assert.Equal(new StatsOutcomes(DoneWithoutHuman: 1, DoneAfterPark: 0, DoneAfterTakeover: 1), stats.Outcomes);
        Assert.Equal(1, stats.BudgetHits);
    }

    [Fact]
    public void Parks_escalations_and_takeovers_are_counted_in_the_state_they_happened_in()
    {
        var a = "T-1/item/1";
        var rows = new List<StatEvent>
        {
            Start(a, Ago(5)),
            Park(a, Ago(5, -10), "produce", ParkReason.NoProgress), Resume(a, Ago(5, -20), "produce"),
            Park(a, Ago(5, -30), "produce", ParkReason.NoProgress), Resume(a, Ago(5, -40), "produce"),
            Move(a, Ago(5, -50), "produce", "verify"),
            TakeOver(a, Ago(5, -60), "verify"), Resume(a, Ago(5, -70), "verify", answered: false),
        };

        var stats = Count(rows);

        var produce = Assert.Single(stats.ByState, s => s.State == "produce");
        Assert.Equal([new ReasonCount("no-progress", 2)], produce.Parks);
        Assert.Equal(1, Assert.Single(stats.ByState, s => s.State == "verify").Takeovers);
        Assert.Equal(new HumanTimeFigure(N: 2, MedianSeconds: 600), stats.HumanTime!.ParksAnswered);
        Assert.Equal(new HumanTimeFigure(N: 1, MedianSeconds: 600), stats.HumanTime.Takeovers);
        Assert.Equal(new HumanTimeFigure(N: 0, MedianSeconds: null), stats.HumanTime.EscalationsDecided);
    }

    [Fact]
    public void A_gate_row_counts_its_refusals_and_its_escalations_by_threshold()
    {
        var a = "T-1/item/1";
        var rows = new List<StatEvent>
        {
            Start(a, Ago(5)),
            Move(a, Ago(5, -10), "produce", "produce", tokens: null, Gate("gate.verify.judge", GateOutcome.Refused)),
            Move(a, Ago(5, -20), "produce", "produce", tokens: null, Gate("gate.verify.judge", GateOutcome.Passed)),
            Escalate(a, Ago(5, -30), "verify") with { Gates = [Gate("gate.verify.judge", GateOutcome.Escalated, threshold: 0.8)] },
        };

        var gate = Assert.Single(Count(rows).ByState, s => s.State == "gate.verify.judge");

        Assert.Equal(new GateRefusals(Refused: 1, Evaluated: 3), gate.GateRefusals);
        Assert.Equal([new ReasonCount("below 0.80", 1)], gate.Escalations);
        Assert.Null(gate.MedianSeconds);
    }

    [Fact]
    public void Run_seconds_state_seconds_and_tokens_are_medians_and_percentiles_of_the_runs()
    {
        var rows = new List<StatEvent>
        {
            Start("T-1/item/1", Ago(5)), Move("T-1/item/1", Ago(5, -10), "produce", "verify", tokens: 100), End("T-1/item/1", Ago(5, -30), "verify", "done", "resolved"),
            Start("T-2/item/1", Ago(4)), Move("T-2/item/1", Ago(4, -20), "produce", "verify", tokens: 300), End("T-2/item/1", Ago(4, -60), "verify", "done", "resolved"),
            Start("T-3/item/1", Ago(3)),
        };

        var stats = Count(rows);

        Assert.Equal(2_700, stats.MedianSeconds);   // 30 and 60 minutes: the median of two is their mean
        Assert.Equal(900, Assert.Single(stats.ByState, s => s.State == "produce").MedianSeconds);
        Assert.Equal(new StatsTokens(P50: 100, P90: 300), stats.Tokens);
    }

    [Fact]
    public void The_four_latest_weeks_are_buckets_oldest_first_by_the_Monday_they_start()
    {
        var rows = new List<StatEvent>
        {
            Start("T-1/item/1", Ago(1)), Start("T-2/item/1", Ago(1)), Park("T-2/item/1", Ago(1, -5), "produce", ParkReason.NeedsAnswer), Resume("T-2/item/1", Ago(1, -10), "produce"),
            Start("T-3/item/1", Ago(10)),
        };

        var buckets = Count(rows).Buckets;

        Assert.Equal(["2026-09-28", "2026-10-05", "2026-10-12", "2026-10-19"], buckets.Select(b => b.WeekStart), StringComparer.Ordinal);   // Now is Thursday 2026-10-22
        Assert.Equal([0, 0, 1, 2], buckets.Select(b => b.Runs));
        Assert.Equal(0.5, buckets[3].HumanRate);
        Assert.Null(buckets[0].HumanRate);
    }

    [Fact]
    public void A_recipe_never_started_as_a_root_has_no_root()
    {
        var rows = new List<StatEvent> { Start("T-1/item/1/step.2", Ago(1), recipe: "workflow.step"), Start("T-2/item/1", Ago(1)) };

        Assert.Equal((false, true), (RecipeStatsCounter.HasRoot(rows, RecipeId.From("workflow.step")), RecipeStatsCounter.HasRoot(rows, _item)));
    }

    [Fact]
    public void A_child_only_recipe_has_null_run_figures()
    {
        var stats = RecipeStats.ForChildOnly(RecipeId.From("workflow.step"), hash: null, StatsWindow.ThirtyDays, Now);

        Assert.Equal((true, null, null, null), (stats.ChildOnly, stats.Runs, stats.HumanRate, stats.Tokens));
        Assert.Empty(stats.ByState);
    }
}
