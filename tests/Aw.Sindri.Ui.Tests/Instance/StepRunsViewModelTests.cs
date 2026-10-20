using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The Step runs in plain words: the current stage's "Attempt N of M" line and the closed Runs list.</summary>
[Lane(TestLane.Unit)]
public sealed class StepRunsViewModelTests
{
    private static readonly DateTimeOffset _start = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    private static StepRun Run(string step, int attempt, int? max, string state, string? runtime = "desk", long? durationMs = null, string? actor = null) =>
        new(attempt.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".1", step, attempt, max, state, runtime, _start, durationMs is null ? null : _start.AddMilliseconds(durationMs.Value), durationMs, Outcome: null, Tokens: null, Actor: actor);

    [Fact]
    public void A_running_attempt_with_a_cap_reads_Attempt_N_of_M_and_who_is_on_it()
    {
        var view = StepRunsViewModel.Of([Run("produce", 2, 3, "running")], "produce");

        Assert.Equal((true, "Attempt 2 of 3, with you"), (view.HasCurrentLine, view.CurrentLine));
    }

    [Fact]
    public void A_running_attempt_with_no_cap_reads_Attempt_N_and_an_agent_when_one_runs_it()
    {
        var view = StepRunsViewModel.Of([Run("build", 2, max: null, state: "running", runtime: "agent")], "build");

        Assert.Equal("Attempt 2, with an agent", view.CurrentLine);
    }

    [Fact]
    public void No_open_run_on_the_current_stage_has_no_current_line()
    {
        var view = StepRunsViewModel.Of([Run("produce", 1, 3, "passed", durationMs: 60_000, actor: "human:cli")], "done");

        Assert.False(view.HasCurrentLine);
        Assert.Equal(string.Empty, view.CurrentLine);
    }

    [Fact]
    public void Each_run_is_a_row_in_plain_words_with_a_matching_accessible_name()
    {
        var view = StepRunsViewModel.Of(
            [Run("produce", 1, 3, "refused", durationMs: 2_700_000, actor: "human:cli"), Run("project-build", 2, 3, "running")],
            "project-build");

        Assert.Equal(
            ["Produce · Attempt 1 of 3 · Refused · 45 min · by you", "Project build · Attempt 2 of 3 · Running · with you"],
            view.Rows.Select(r => r.Text),
            StringComparer.Ordinal);
        Assert.All(view.Rows, r => Assert.StartsWith(r.Text.Split(" · ")[0], r.Name, StringComparison.Ordinal));
        Assert.Equal("Produce, attempt 1 of 3, refused, took 45 min, by you", view.Rows[0].Name);
    }

    [Theory]
    [InlineData("passed", "Passed")]
    [InlineData("failed", "Failed")]
    [InlineData("refused", "Refused")]
    [InlineData("cancelled", "Cancelled")]
    [InlineData("abandoned", "Abandoned")]
    [InlineData("something-new", "Something new")]
    public void Every_state_is_a_plain_word_and_an_unknown_one_is_made_readable(string state, string word)
    {
        var view = StepRunsViewModel.Of([Run("check", 1, max: null, state: state, durationMs: 120_000, actor: "human:cli")], "check");

        Assert.Equal($"Check · Attempt 1 · {word} · 2 min · by you", view.Rows[0].Text);
    }

    [Theory]
    [InlineData(20_000, "under a minute")]
    [InlineData(240_000, "4 min")]
    [InlineData(4_500_000, "1 h 15 min")]
    public void A_duration_reads_in_minutes(long ms, string words)
    {
        var view = StepRunsViewModel.Of([Run("check", 1, max: null, state: "passed", durationMs: ms, actor: "agent:cursor")], "x");

        Assert.Contains(" · " + words + " · by an agent", view.Rows[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_closed_run_with_no_recorded_actor_says_nobody_closed_it()
    {
        var view = StepRunsViewModel.Of([Run("check", 1, max: null, state: "passed", runtime: "agent", durationMs: 60_000)], "x");

        Assert.EndsWith("1 min", view.Rows[0].Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("system:timer")]
    [InlineData("timer")]
    public void A_closed_run_with_an_unrecognised_actor_has_no_by_part(string actor)
    {
        var view = StepRunsViewModel.Of([Run("check", 1, max: null, state: "failed", runtime: "desk", durationMs: 60_000, actor: actor)], "x");

        Assert.DoesNotContain("by ", view.Rows[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain("by ", view.Rows[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public void A_closed_run_closed_by_a_session_reads_by_an_agent()
    {
        var view = StepRunsViewModel.Of([Run("check", 1, max: null, state: "passed", runtime: "desk", durationMs: 60_000, actor: "session:abc")], "x");

        Assert.EndsWith("by an agent", view.Rows[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void An_open_run_with_no_runtime_and_no_actor_names_nobody()
    {
        var view = StepRunsViewModel.Of([Run("check", 1, 3, "running", runtime: null)], "check");

        Assert.Equal("Attempt 1 of 3", view.CurrentLine);
        Assert.Equal("Check · Attempt 1 of 3 · Running", view.Rows[0].Text);
    }

    [Theory]
    [InlineData("desk")]
    [InlineData("agent")]
    public void An_instance_the_engine_owns_drops_with_whom_from_the_run_line(string runtime)
    {
        var view = StepRunsViewModel.Of([Run("produce", 2, 3, "running", runtime: runtime)], "produce", engineOwned: true);

        Assert.Equal("Attempt 2 of 3", view.CurrentLine);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void No_runs_draws_nothing_not_even_from_an_older_server(bool missing)
    {
        var view = StepRunsViewModel.Of(missing ? null : [], "produce");

        Assert.Equal((false, false, 0), (view.HasCurrentLine, view.HasRows, view.Rows.Count));
    }

    [Fact]
    public void The_heading_is_Runs() => Assert.Equal("Runs", StepRunsViewModel.Of([], "x").Heading);
}
