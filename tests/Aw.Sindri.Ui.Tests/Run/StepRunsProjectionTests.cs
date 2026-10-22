using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.Tests.Run;

/// <summary>The inspector's step timeline from the served Step runs (F3 spec 3 §4, slice 4): tries, state words, elapsed time and who ran each.</summary>
[Lane(TestLane.Unit)]
public sealed class StepRunsProjectionTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static StepRun Run(string state, int attempt = 1, int? max = null, string? runtime = "agent", string? actor = null, long? ms = null, DateTimeOffset? ended = null, string? outcome = null, int? tokens = null, string step = "produce") =>
        new(string.Create(CultureInfo.InvariantCulture, $"{step}.{attempt}"), step, attempt, max, state, runtime, _now.AddMinutes(-30), ended, ms, outcome, tokens, actor);

    private static RunTimelineRow Only(params StepRun[] runs) => Assert.Single(RunTimeline.Of(runs, "produce", _now));

    [Fact]
    public void A_refused_run_reads_failed_with_the_check_sentence()
    {
        var row = Only(Run("refused", ms: 60_000, ended: _now.AddMinutes(-29), actor: "agent:session"));

        Assert.Equal(RunNodeState.Failed, row.State);
        Assert.Equal(Labels.RunWordFailed, row.Word);
        Assert.Equal(Labels.RunRefusedByCheck, row.Note);
    }

    [Fact]
    public void A_failed_run_reads_failed_and_names_no_check()
    {
        var row = Only(Run("failed", ms: 60_000, ended: _now.AddMinutes(-29)));

        Assert.Equal((RunNodeState.Failed, string.Empty), (row.State, row.Note));
    }

    [Fact]
    public void A_cancelled_and_an_abandoned_run_read_cancelled()
    {
        var rows = RunTimeline.Of([Run("cancelled"), Run("abandoned", attempt: 2)], "produce", _now);

        Assert.All(rows, r => Assert.Equal(RunNodeState.Cancelled, r.State));
    }

    [Fact]
    public void The_try_reads_n_of_max()
    {
        Assert.Equal("Try 2 of 3", Only(Run("passed", attempt: 2, max: 3)).TryText);
        Assert.Equal("Try 1", Only(Run("passed")).TryText);
    }

    [Fact]
    public void A_running_run_counts_its_elapsed_time_from_when_it_started()
    {
        var row = Only(Run("running"));

        Assert.Equal(RunNodeState.Running, row.State);
        Assert.Equal(Labels.RunRunningFor("30m"), row.TimesText);
    }

    [Fact]
    public void A_closed_run_says_how_long_it_took()
    {
        var row = Only(Run("passed", ms: 4 * 60_000, ended: _now.AddMinutes(-26)));

        Assert.Equal(Labels.RunTook(Labels.RunDuration(4 * 60_000)), row.TimesText);
    }

    [Fact]
    public void A_closed_run_with_no_duration_shows_the_no_times_copy() =>
        Assert.Equal(Labels.RunNoTimes, Only(Run("passed")).TimesText);

    [Theory]
    [InlineData("agent:session", "agent", "AI agent")]
    [InlineData("human:owner", "agent", "You")]
    [InlineData(null, "desk", "You")]
    [InlineData(null, null, "")]
    public void The_actor_reads_as_an_AI_agent_or_you(string? actor, string? runtime, string expected)
    {
        var state = actor is null ? "running" : "passed";

        Assert.Equal(expected, Only(Run(state, runtime: runtime, actor: actor)).Who);
    }

    [Fact]
    public void Tokens_read_as_used()
    {
        Assert.Equal("61k used", Only(Run("passed", tokens: 61_000)).TokensText);
        Assert.Equal(string.Empty, Only(Run("passed")).TokensText);
    }

    [Fact]
    public void Only_the_asked_step_is_listed_in_the_order_of_its_tries()
    {
        var rows = RunTimeline.Of([Run("passed", attempt: 2), Run("passed", step: "check"), Run("failed")], "produce", _now);

        Assert.Equal(["Try 1", "Try 2"], rows.Select(r => r.TryText), StringComparer.Ordinal);
    }
}
