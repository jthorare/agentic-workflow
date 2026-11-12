using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Failures;

namespace Aw.Sindri.Tests.Graph.Failures;

/// <summary>
/// P3e (#3196, Workflows page spec §6.1, E9): one test per row of the failure-to-sentence table. Each asserts the row exists, the exact owner-facing sentence,
/// that the fix is what the table names, and for an SND- code that its ErrorDefinition exists with the same code. Placeholders are filled from step titles and config, never node ids.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class FailureSentenceTableTests
{
    private static Dictionary<string, string> With(params (string Name, string Value)[] values) => values.ToDictionary(static v => v.Name, static v => v.Value, StringComparer.Ordinal);

    private static FailureSentence Row(string code)
    {
        var row = FailureSentences.Find(code);
        Assert.NotNull(row);
        Assert.Equal(code, row.Code);
        if (code.StartsWith("SND-", StringComparison.Ordinal))
        {
            Assert.Contains(SindriErrors.All, e => string.Equals(e.Code, code, StringComparison.Ordinal));
        }

        return row;
    }

    [Fact]
    public void GRAPH_CONFIG_001_says_the_step_needs_a_repo()
    {
        var row = Row("GRAPH-CONFIG-001");

        Assert.Equal("Review needs a repo.", FailureSentences.Say(row.Code, With(("step", "Review"))));
        Assert.NotNull(row.FixLabel);
        Assert.StartsWith("sindri wf edit ", row.FixCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void GRAPH_NAME_001_says_the_connection_is_not_on_this_box()
    {
        var row = Row("GRAPH-NAME-001");

        Assert.Equal("work-github is not a connection on this box.", FailureSentences.Say(row.Code, With(("connection", "work-github"))));
        Assert.Equal("Connect GitHub", row.FixLabel);
        Assert.Contains("connection add", row.FixCommand, StringComparison.Ordinal);
        Assert.Contains("--preset github", row.FixCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void GRAPH_NAME_002_says_the_connection_could_not_be_checked_yet()
    {
        var row = Row("GRAPH-NAME-002");

        Assert.Equal("Couldn't check the connection yet.", FailureSentences.Say(row.Code, With()));
        Assert.Equal("Retry", row.FixLabel);
        Assert.Contains("wf validate", row.FixCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void GRAPH_TRIGGER_001_says_a_workflow_has_one_trigger()
    {
        var row = Row("GRAPH-TRIGGER-001");

        Assert.Equal("A workflow has one trigger.", FailureSentences.Say(row.Code, With()));
        Assert.Equal("Change trigger", row.FixLabel);
        Assert.Contains("replace_node", row.FixCommand, StringComparison.Ordinal);
    }

    // SND-GRAPH-112 is added by the replace_node phase (E3); this row checks the catalog sentence only, so it needs no definition here
    [Fact]
    public void SND_GRAPH_112_says_only_the_trigger_can_be_replaced()
    {
        var row = FailureSentences.Find("SND-GRAPH-112");

        Assert.NotNull(row);
        Assert.Equal("Only the trigger can be replaced.", FailureSentences.Say("SND-GRAPH-112", With()));
        Assert.Null(row.FixLabel);
        Assert.Null(row.FixCommand);
    }

    [Fact]
    public void SND_GRAPH_103_quotes_what_the_model_said_and_offers_retry_and_open_the_step()
    {
        var row = Row("SND-GRAPH-103");

        Assert.Equal(
            "The model answered with text, not the JSON this step asks for: \"Usage limit reached\".",
            FailureSentences.Say(row.Code, With(("answer", "Usage limit reached"))));
        Assert.Equal("Retry with model", row.FixLabel);
        Assert.Contains("--with-model", row.FixCommand, StringComparison.Ordinal);
        Assert.Contains("wf test", row.FixCommand, StringComparison.Ordinal);
        Assert.Equal("Open {step}", row.SecondaryLabel);
    }

    [Fact]
    public void SND_GRAPH_103_cuts_the_quoted_answer_at_200_characters()
    {
        var said = FailureSentences.Say("SND-GRAPH-103", With(("answer", new string('q', 500))));

        Assert.Contains(new string('q', 200), said, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('q', 201), said, StringComparison.Ordinal);
    }

    [Fact]
    public void SND_GRAPH_113_names_the_step_the_field_and_the_empty_reference()
    {
        var row = Row("SND-GRAPH-113");

        Assert.Equal(
            "Review: \"What should it do?\" came out empty. trigger.pr.body had no value in this input.",
            FailureSentences.Say(row.Code, With(("step", "Review"), ("field", "What should it do?"), ("reference", "trigger.pr.body"))));
        Assert.Equal("Edit {field}", row.FixLabel);
        Assert.Contains("wf edit", row.FixCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void SND_LAUNCH_003_says_the_agent_for_the_step_is_not_available()
    {
        var row = Row("SND-LAUNCH-003");

        Assert.Equal(
            "The agent for step Plan isn't available: its CLI is not installed or the target is not in the catalog.",
            FailureSentences.Say(row.Code, With(("step", "Plan"))));
        Assert.Equal("Choose an agent", row.FixLabel);
        Assert.Contains("workflow.agent.target", row.FixCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void SND_LAUNCH_013_says_the_agent_entry_cannot_express_the_step()
    {
        var row = Row("SND-LAUNCH-013");

        Assert.Equal("The agent entry can't express Plan's model or policy.", FailureSentences.Say(row.Code, With(("step", "Plan"))));
        Assert.NotNull(row.FixLabel);
    }

    [Fact]
    public void SND_GRAPH_105_says_the_workflow_is_not_on()
    {
        var row = Row("SND-GRAPH-105");

        Assert.Equal("This workflow is not on.", FailureSentences.Say(row.Code, With()));
        Assert.Equal("Activate", row.FixLabel);
        Assert.Contains("wf activate", row.FixCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void SND_GRAPH_106_says_a_manual_runs_input_must_be_a_JSON_object()
    {
        var row = Row("SND-GRAPH-106");

        Assert.Equal("A manual run's input must be a JSON object.", FailureSentences.Say(row.Code, With()));
        Assert.Equal("Edit the sample", row.FixLabel);
        Assert.Contains("--trigger", row.FixCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void SND_GRAPH_107_says_the_run_already_ended_and_has_no_fix()
    {
        var row = Row("SND-GRAPH-107");

        Assert.Equal("This run already ended.", FailureSentences.Say(row.Code, With()));
        Assert.Null(row.FixLabel);
        Assert.Null(row.FixCommand);
    }

    [Fact]
    public void SND_GRAPH_108_says_the_step_output_was_cut_at_64_KB()
    {
        var row = Row("SND-GRAPH-108");

        Assert.Equal("The output of Fetch was cut at 64 KB, so a later step can't read it.", FailureSentences.Say(row.Code, With(("step", "Fetch"))));
        Assert.Equal("Open the step", row.FixLabel);
    }

    [Fact]
    public void SND_GRAPH_109_says_a_run_cannot_start_the_workflow_again()
    {
        var row = Row("SND-GRAPH-109");

        Assert.Equal("A run of this workflow can't start the workflow again.", FailureSentences.Say(row.Code, With()));
        Assert.Equal("Open the step that does", row.FixLabel);
    }

    [Fact]
    public void SND_GRAPH_007_says_it_changed_elsewhere()
    {
        var row = Row("SND-GRAPH-007");

        Assert.Equal("Changed elsewhere.", FailureSentences.Say(row.Code, With()));
        Assert.Equal("Reload", row.FixLabel);
        Assert.Contains("wf show", row.FixCommand, StringComparison.Ordinal);
    }

    [Fact]
    public void SND_API_001_says_sindri_is_not_running()
    {
        var row = Row("SND-API-001");

        Assert.Equal("Sindri is not running.", FailureSentences.Say(row.Code, With()));
        Assert.Equal("Start Sindri", row.FixLabel);
        Assert.Equal("sindri serve", row.FixCommand);
    }

    [Fact]
    public void SND_CODE_001_says_the_step_does_not_compile_and_shows_the_errors()
    {
        var row = Row("SND-CODE-001");

        Assert.Equal("Check doesn't compile: line 3, column 5: CS1002 ; expected.", FailureSentences.Say(row.Code, With(("step", "Check"), ("message", "line 3, column 5: CS1002 ; expected"))));
        Assert.Equal("Open {step}", row.FixLabel);
    }

    [Fact]
    public void SND_CODE_002_says_the_step_ran_past_its_time_limit()
    {
        var row = Row("SND-CODE-002");

        Assert.Equal("Check ran past its time limit.", FailureSentences.Say(row.Code, With(("step", "Check"))));
        Assert.Equal("Open {step}", row.FixLabel);
    }

    [Fact]
    public void SND_CODE_003_says_the_step_used_more_memory_than_its_limit()
    {
        var row = Row("SND-CODE-003");

        Assert.Equal("Check used more memory than its limit.", FailureSentences.Say(row.Code, With(("step", "Check"))));
        Assert.Equal("Open {step}", row.FixLabel);
    }

    [Fact]
    public void SND_CODE_004_says_the_step_stopped_with_the_exception_message()
    {
        var row = Row("SND-CODE-004");

        Assert.Equal("Check stopped: Index was outside the bounds of the array.", FailureSentences.Say(row.Code, With(("step", "Check"), ("message", "Index was outside the bounds of the array"))));
        Assert.Equal("Open {step}", row.FixLabel);
    }

    [Fact]
    public void SND_CODE_005_says_the_runner_could_not_start_shows_the_reason_and_offers_no_install_button()
    {
        var row = Row("SND-CODE-005");

        Assert.Equal("Sindri can't run code steps: the code runner couldn't start.\nthe runner binary is not at /x/sindri-code-runner", FailureSentences.Say(row.Code, With(("message", "the runner binary is not at /x/sindri-code-runner"))));
        Assert.Null(row.FixLabel);
        Assert.Null(row.FixCommand);
    }

    [Fact]
    public void A_long_multi_line_message_is_cut_to_one_line_of_200_characters()
    {
        var message = "line 1, column 2: CS1002 first\nline 3, column 4: CS1002 " + new string('x', 300);

        var said = FailureSentences.Say("SND-CODE-001", With(("step", "Check"), ("message", message)));

        Assert.DoesNotContain('\n', said);
        Assert.Equal("Check doesn't compile: ".Length + 200 + "….".Length, said.Length);
        Assert.EndsWith("….", said, StringComparison.Ordinal);
    }

    [Fact]
    public void A_message_that_ends_in_a_period_does_not_double_it()
        => Assert.Equal("Check stopped: Boom.", FailureSentences.Say("SND-CODE-004", With(("step", "Check"), ("message", "Boom."))));

    [Fact]
    public void A_message_that_ends_in_a_question_mark_keeps_it_and_adds_no_period()
        => Assert.Equal("Check stopped: Is it null?", FailureSentences.Say("SND-CODE-004", With(("step", "Check"), ("message", "Is it null?"))));

    [Theory]
    [InlineData("SND-CODE-001", "Check doesn't compile.")]
    [InlineData("SND-CODE-004", "Check stopped.")]
    [InlineData("SND-CODE-005", "Sindri can't run code steps: the code runner couldn't start.")]
    public void An_empty_message_drops_its_lead_in(string code, string expected)
        => Assert.Equal(expected, FailureSentences.Say(code, With(("step", "Check"), ("message", "  "))));

    [Fact]
    public void SND_CODE_005_cuts_its_message_to_one_line_of_200_characters_too()
    {
        var said = FailureSentences.Say("SND-CODE-005", With(("message", new string('x', 150) + "\n" + new string('y', 150))));

        var reason = said.Split('\n')[1];
        Assert.Equal(201, reason.Length);
        Assert.EndsWith("…", reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SND-CODE-001")]
    [InlineData("SND-CODE-002")]
    [InlineData("SND-CODE-003")]
    [InlineData("SND-CODE-004")]
    public void An_SND_CODE_sentence_names_the_step_by_its_title_never_a_node_id(string code)
    {
        var said = FailureSentences.Say(code, With(("step", "Nightly check"), ("message", "boom")));

        Assert.StartsWith("Nightly check ", said, StringComparison.Ordinal);
        Assert.DoesNotContain("node '", said, StringComparison.Ordinal);
    }

    [Fact]
    public void A_placeholder_with_no_value_fails_instead_of_leaving_braces_in_the_sentence()
        => Assert.Throws<InvalidOperationException>(() => FailureSentences.Say("GRAPH-CONFIG-001", With()));

    [Fact]
    public void An_unknown_code_has_no_row()
        => Assert.Null(FailureSentences.Find("SND-NOPE-999"));
}
