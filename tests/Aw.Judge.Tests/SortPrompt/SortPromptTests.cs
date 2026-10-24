using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Providers;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.SortPrompt;

// The prompt sorter (cutover spec §5.2, unit 6.1): one prompt in, one 'prompt-sort' decision with its axes and run out.
public sealed class SortPromptTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task SortPrompt_records_the_decision_its_axes_and_its_run_and_adds_no_context_while_every_scaffold_is_off()
    {
        var harness = new SortPromptHarness();

        var result = await harness.Service.Handle(A.SortPromptRequest().Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Context);
        var decision = Assert.Single(harness.Log.Decisions);
        Assert.Equal("prompt-sort", decision.Question);
        var saved = Assert.Single(harness.Store.Records);
        Assert.Equal(decision.Id, saved.Decision);
        Assert.Equal(11, saved.Axes.Count);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("<system-reminder>machine text</system-reminder>")]
    [InlineData("/clear")]
    [InlineData("This session is being continued from a previous conversation")]
    [InlineData("yes")]
    public async Task SortPrompt_skips_machine_text_slash_commands_and_confirmations_without_recording(string prompt)
    {
        var harness = new SortPromptHarness();

        var result = await harness.Service.Handle(A.SortPromptRequest().Prompt(prompt).Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Context);
        Assert.Empty(harness.Log.Decisions);
        Assert.Empty(harness.Jev.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SortPrompt_asks_jev_once_for_all_eleven_axes()
    {
        var harness = new SortPromptHarness();

        _ = await harness.Service.Handle(A.SortPromptRequest().Build(), _ct);

        var batch = Assert.Single(harness.Jev.Asked);
        Assert.Equal(11, batch.Questions.Count);
        Assert.Equal(9, batch.Questions.OfType<NoulBatchQuestion>().Count());
        Assert.Equal(2, batch.Questions.OfType<ScoreBatchQuestion>().Count());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SortPrompt_with_jev_excluded_by_the_allowlist_sorts_by_heuristics_alone()
    {
        var harness = new SortPromptHarness();

        var result = await harness.Service.Handle(A.SortPromptRequest().Allowing(ProviderName.Rules).Build(), _ct);

        Assert.True(result.IsSuccess);
        var run = Assert.Single(harness.Store.Records).Run;
        Assert.Equal(("heuristic-only", "no-jev"), (run.Mode, run.Reason));
        Assert.Empty(harness.Jev.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SortPrompt_redacts_a_secret_before_it_reaches_jev_the_decision_details_and_the_session_file()
    {
        var harness = new SortPromptHarness();

        _ = await harness.Service.Handle(A.SortPromptRequest().Prompt("deploy with " + SampleSecrets.GitHubToken).Build(), _ct);

        Assert.DoesNotContain(SampleSecrets.GitHubToken, Assert.Single(harness.Jev.Asked).State?.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SampleSecrets.GitHubToken, Assert.Single(harness.Log.Decisions).Details?.InputJson, StringComparison.Ordinal);
        Assert.DoesNotContain(SampleSecrets.GitHubToken, System.Text.Json.JsonSerializer.Serialize(harness.Sessions.Sessions.Values.Select(s => s.LastFired)), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SortPrompt_counts_the_session_s_prompts()
    {
        var harness = new SortPromptHarness();

        _ = await harness.Service.Handle(A.SortPromptRequest().Build(), _ct);
        _ = await harness.Service.Handle(A.SortPromptRequest().Build(), _ct);

        Assert.Equal(2, harness.Sessions.Sessions["sess-1"].Prompts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SortPrompt_keeps_no_session_state_when_the_hook_had_no_session()
    {
        var harness = new SortPromptHarness();

        _ = await harness.Service.Handle(A.SortPromptRequest().WithoutSession().Build(), _ct);

        Assert.Empty(harness.Sessions.Sessions);
        _ = Assert.Single(harness.Log.Decisions);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SortPrompt_prints_a_scaffold_note_only_when_its_switch_is_on_and_jev_decided_its_axis()
    {
        var harness = new SortPromptHarness(SortPromptHarness.JevSaying(new Dictionary<string, double>(StringComparer.Ordinal) { ["is_bug_report"] = 0.95, ["is_task"] = 0.9 }));
        harness.SwitchOn("bugfix");

        var result = await harness.Service.Handle(A.SortPromptRequest().Prompt("the login page crashes when I press submit").Build(), _ct);

        Assert.Contains("/bugFixOrchestrator", result.Value.Context, StringComparison.Ordinal);
        Assert.Equal(["bugfix"], Assert.Single(harness.Store.Records).Run.Fired);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SortPrompt_reports_what_would_have_fired_when_the_switch_is_off()
    {
        var harness = new SortPromptHarness(SortPromptHarness.JevSaying(new Dictionary<string, double>(StringComparer.Ordinal) { ["is_bug_report"] = 0.95 }));

        var result = await harness.Service.Handle(A.SortPromptRequest().Prompt("the login page crashes when I press submit").Build(), _ct);

        Assert.Null(result.Value.Context);
        var run = Assert.Single(harness.Store.Records).Run;
        Assert.Equal(["bugfix"], run.WouldFire);
        Assert.Empty(run.Fired);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SortPrompt_fires_nothing_on_a_heuristic_only_run_even_with_every_switch_on()
    {
        var harness = new SortPromptHarness();
        harness.SwitchOn("brief", "bugfix", "ui-evidence", "plan-first");

        var result = await harness.Service.Handle(A.SortPromptRequest().Prompt("the login page crashes when I press submit").Build(), _ct);

        Assert.Null(result.Value.Context);
        Assert.Contains("heuristic-only", Assert.Single(Assert.Single(harness.Store.Records).Run.Suppressed), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SortPrompt_does_not_repeat_a_scaffold_inside_its_cooldown()
    {
        var harness = new SortPromptHarness(SortPromptHarness.JevSaying(new Dictionary<string, double>(StringComparer.Ordinal) { ["is_bug_report"] = 0.95 }));
        harness.SwitchOn("bugfix");
        var request = A.SortPromptRequest().Prompt("the login page crashes when I press submit").Build();

        var first = await harness.Service.Handle(request, _ct);
        var second = await harness.Service.Handle(request, _ct);

        Assert.NotNull(first.Value.Context);
        Assert.Null(second.Value.Context);
        Assert.Contains("cooldown", harness.Store.Records.Last().Run.Suppressed.Single(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SortPrompt_sets_the_ui_evidence_requirement_the_done_gate_reads_and_a_judged_non_ui_task_clears_it()
    {
        var harness = new SortPromptHarness(SortPromptHarness.JevSaying(new Dictionary<string, double>(StringComparer.Ordinal) { ["is_task"] = 0.9, ["touches_ui"] = 0.95 }));
        harness.SwitchOn("ui-evidence");
        _ = await harness.Service.Handle(A.SortPromptRequest().Prompt("redesign the settings page layout").Build(), _ct);
        Assert.True(harness.Sessions.Sessions["sess-1"].Requirements.UiEvidence);

        var followUp = new SortPromptHarness(SortPromptHarness.JevSaying(new Dictionary<string, double>(StringComparer.Ordinal) { ["is_task"] = 0.9, ["touches_ui"] = 0.05 }), harness.Sessions);
        _ = await followUp.Service.Handle(A.SortPromptRequest().Prompt("add a retry to the sync job").Build(), _ct);
        Assert.False(followUp.Sessions.Sessions["sess-1"].Requirements.UiEvidence);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SortPrompt_records_a_failure_when_jev_errors_and_still_sorts_by_heuristics()
    {
        var harness = new SortPromptHarness(new FakeBatchClassifier(_ => new ProviderErrored("http-500")));

        var result = await harness.Service.Handle(A.SortPromptRequest().Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("http-500", Assert.Single(harness.Log.Failures).ReasonCode);
        Assert.Equal("heuristic-only:http-500", Assert.Single(harness.Log.Decisions).ReasonCode);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task SortPrompt_does_nothing_when_the_sorter_is_disabled_in_the_config()
    {
        var harness = new SortPromptHarness();
        harness.Config.Stored = JudgeConfig.Default with { PromptSort = new PromptSortConfig(Enabled: false) };

        var result = await harness.Service.Handle(A.SortPromptRequest().Build(), _ct);

        Assert.True(result.IsSuccess);
        Assert.Empty(harness.Log.Decisions);
        Assert.Empty(harness.Jev.Asked);
    }
}
