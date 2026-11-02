using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The instance frame's Model picks table (model-pick spec §6.1, M-8): each cell a served field in words, "choosing model…", "cost unknown", the total line, escalated rows, the Why panel and the accessible names.</summary>
[Lane(TestLane.Unit)]
public sealed class ModelPicksViewModelTests
{
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static string StepName(string state) => state == "questions" ? "Write questions for you" : "Draft";

    private static PickedView Picked(string? model = "claude-haiku-4-5", string by = "jev", bool unusual = false, string? from = null, string? kind = null, string? decision = "JD-1") =>
        new("claude-code", model, by, by == "rules" ? "timeout" : null, decision, ["m1", "m2"], [new PickedFileView("src/App.cs", 1200)], [new OmittedFileView("big.log", 500000, "cap")], JevMs: 2100, JevCalls: 1, ContentSent: true, Unusual: unusual, EscalatedFrom: from, EscalatedFromKind: kind);

    private static LaunchPickRow Row(string state = "questions", int escalation = 0, PickedView? pick = null, long input = 30_000, long output = 11_000, decimal? cost = 0.18m,
        string outcome = "passed", string why = "Picked by Jev: the cheapest model it expects to pass.", bool picking = false) =>
        new("LCH-1", "ITM-1", "ITM-1/step/1", state, 1, escalation, picking, picking ? null : pick ?? Picked(), input, output, cost, outcome, why, A.Noon);

    private ModelPicksViewModel Table(params LaunchPickRow[] rows) => Of(new LaunchPicksView(rows));

    private ModelPicksViewModel Of(LaunchPicksView? view) => ModelPicksViewModel.Of(view, StepName, _clipboard, _clock);

    [Fact]
    public void A_picked_run_reads_step_short_model_tokens_cost_and_the_served_why()
    {
        var row = Table(Row()).Rows.Single();

        Assert.Equal(("Write questions for you", "Haiku", "41k", "$0.18", "Picked by Jev: the cheapest model it expects to pass.", "passed"),
            (row.Step, row.Model, row.Tokens, row.Cost, row.Why, row.Outcome));
    }

    [Fact]
    public void A_run_still_being_picked_says_choosing_model_and_asking_Jev_and_is_announced()
    {
        var table = Table(Row(picking: true, input: 0, output: 0, cost: null, outcome: "running", why: string.Empty));
        var row = table.Rows.Single();

        Assert.Equal(("choosing model…", "asking Jev", true, "Model: choosing model…"), (row.Model, row.Why, row.IsPicking, row.ModelName));
        Assert.Equal("choosing model…", table.Announcement);
    }

    [Fact]
    public void A_settled_table_announces_nothing() =>
        Assert.Equal(string.Empty, Table(Row()).Announcement);

    [Fact]
    public void A_model_with_no_price_says_cost_unknown_and_the_total_says_cost_is_partial()
    {
        var table = Table(Row(cost: 0.20m), Row(state: "draft", pick: Picked("some-new-model"), cost: null));

        Assert.Equal("cost unknown", table.Rows[1].Cost);
        Assert.Equal("2 runs, 82k tokens, cost partial: 1 model has no price", table.TotalLine);
    }

    [Fact]
    public void The_total_line_sums_runs_tokens_and_cost()
    {
        var table = Table(Row(cost: 0.18m), Row(state: "draft", input: 40_000, output: 15_000, cost: 0.43m));

        Assert.Equal("2 runs, 96k tokens, $0.61", table.TotalLine);
    }

    [Fact]
    public void One_run_is_not_plural() =>
        Assert.Equal("1 run, 41k tokens, $0.18", Table(Row()).TotalLine);

    [Fact]
    public void An_escalated_step_has_one_row_per_run_each_with_its_outcome_word()
    {
        var table = Table(
            Row(outcome: "refused", input: 6_000, output: 2_000, cost: 0.01m),
            Row(escalation: 1, pick: Picked("claude-sonnet-4-5", by: "rules", from: "claude-haiku-4-5", kind: "refused"), outcome: "passed", why: "Retried on claude-sonnet-4-5 after claude-haiku-4-5 was refused.", cost: 0.14m));

        Assert.Equal(["Write questions for you", "Write questions for you (retry 1)"], table.Rows.Select(r => r.Step), StringComparer.Ordinal);
        Assert.Equal(["refused", "passed"], table.Rows.Select(r => r.Outcome), StringComparer.Ordinal);
        Assert.Equal([false, true], table.Rows.Select(r => r.IsRetry));
        Assert.Equal("Retried on claude-sonnet-4-5 after claude-haiku-4-5 was refused.", table.Rows[1].Why);
    }

    [Fact]
    public void An_unusual_pick_draws_the_served_sentence_unchanged()
    {
        const string why = "Picked by Jev: the cheapest model it expects to pass. Unusual: claude-haiku-4-5 failed most of its recent runs on this step.";

        Assert.Equal(why, Table(Row(pick: Picked(unusual: true), why: why)).Rows.Single().Why);
    }

    [Fact]
    public void A_run_on_the_agents_own_default_says_agent_default()
    {
        var row = Table(Row(pick: Picked(model: null, by: "default", decision: null), why: "This agent reports no models, so it ran on its own default.")).Rows.Single();

        Assert.Equal(("agent default", "Model: agent default"), (row.Model, row.ModelName));
    }

    [Theory]
    [InlineData("jev", "Model: Haiku, picked by Jev")]
    [InlineData("constraint", "Model: Haiku, the only model allowed")]
    [InlineData("rules", "Model: Haiku, picked by Sindri's rules")]
    public void The_model_cell_names_who_picked_it(string by, string name) =>
        Assert.Equal(name, Table(Row(pick: Picked(by: by))).Rows.Single().ModelName);

    [Fact]
    public void The_row_name_reads_step_model_tokens_cost_why_then_outcome_and_starts_with_the_visible_step()
    {
        var row = Table(Row()).Rows.Single();

        Assert.Equal("Write questions for you, Haiku, 41k tokens, $0.18, Picked by Jev: the cheapest model it expects to pass., passed", row.Name);
        Assert.StartsWith(row.Step, row.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void The_footer_says_how_often_a_state_was_retried_on_a_stronger_model()
    {
        var table = Of(new LaunchPicksView([Row()], [new StateEscalationView("questions", "claude-haiku-4-5", 20, 3, 0.15, "retried on a stronger model in 3 of 20 runs")]));

        Assert.Equal(["Write questions for you on Haiku: retried on a stronger model in 3 of 20 runs"], table.EscalationLines);
    }

    [Fact]
    public void The_why_panel_lists_candidates_files_left_out_and_the_judge_why_command_with_no_file_text()
    {
        var panel = Table(Row()).Rows.Single().Panel!;

        Assert.Equal(["m1", "m2"], panel.Candidates);
        Assert.Equal(["src/App.cs · 1200 characters"], panel.Files);
        Assert.Equal(["not shown: big.log, over the size limit"], panel.Omitted);
        Assert.Equal("judge why JD-1", panel.Command);
    }

    [Fact]
    public async Task The_why_panels_copy_puts_the_command_on_the_clipboard()
    {
        var panel = Table(Row()).Rows.Single().Panel!;

        await panel.CopyCommand!.Run(panel.Command, TestContext.Current.CancellationToken);

        Assert.Equal("judge why JD-1", _clipboard.Text);
    }

    [Fact]
    public void A_pick_Jev_was_not_asked_about_has_a_panel_without_a_command()
    {
        var panel = Table(Row(pick: Picked(by: "constraint", decision: null))).Rows.Single().Panel!;

        Assert.Equal((string.Empty, null), (panel.Command, panel.CopyCommand));
    }

    [Fact]
    public void A_failed_read_or_an_empty_answer_draws_nothing()
    {
        Assert.False(Of(view: null).HasRows);
        Assert.False(Of(new LaunchPicksView([])).HasRows);
    }

    [Fact]
    public void The_same_picks_read_again_have_the_same_signature_and_a_new_pick_changes_it()
    {
        Assert.Equal(Table(Row()).Signature, Table(Row()).Signature);
        Assert.NotEqual(Table(Row()).Signature, Table(Row(), Row(state: "draft")).Signature, StringComparer.Ordinal);
        Assert.NotEqual(Table(Row(picking: true)).Signature, Table(Row()).Signature, StringComparer.Ordinal);
    }
}
