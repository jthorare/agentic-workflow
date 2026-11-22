using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Failures;

namespace Aw.Sindri.Tests.Graph.Failures;

/// <summary>
/// #3304 (Workflows page spec §6.1, §7.3): a failed node run's error ("&lt;code&gt;: &lt;message&gt;") becomes the §6.1 sentence with the step named by its title,
/// the fix with the title filled in, and the engine's text kept whole for Details.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class StepFailuresTests
{
    [Fact]
    public void SND_CODE_004_says_the_step_stopped_with_the_exception_message_and_drops_the_exception_type()
    {
        const string Error = "SND-CODE-004: InvalidOperationException: repo is 812 MB, over the 500 MB budget";

        var failure = StepFailures.Describe("Check", Error);

        Assert.NotNull(failure);
        Assert.Equal("SND-CODE-004", failure.Code);
        Assert.Equal("Check stopped: repo is 812 MB, over the 500 MB budget.", failure.Sentence);
        Assert.Equal("Open Check", failure.FixLabel);
        Assert.Equal(Error, failure.Detail);
    }

    [Fact]
    public void SND_CODE_004_drops_a_namespaced_exception_type_too()
        => Assert.Equal("Check stopped: disk full.", StepFailures.Describe("Check", "SND-CODE-004: System.IO.IOException: disk full")?.Sentence);

    [Fact]
    public void SND_CODE_004_keeps_a_message_whose_lead_is_not_an_exception_type()
        => Assert.Equal(
            "Check stopped: the result is not JSON: 'x' is an invalid start of a value.",
            StepFailures.Describe("Check", "SND-CODE-004: the result is not JSON: 'x' is an invalid start of a value.")?.Sentence);

    [Fact]
    public void SND_CODE_004_with_a_bare_exception_type_and_no_message_reads_as_stopped()
        => Assert.Equal("Check stopped.", StepFailures.Describe("Check", "SND-CODE-004: NullReferenceException: ")?.Sentence);

    [Fact]
    public void SND_CODE_001_says_the_step_does_not_compile_and_lists_the_errors()
    {
        var failure = StepFailures.Describe("Check", "SND-CODE-001: line 3, column 5: CS1002 ; expected");

        Assert.NotNull(failure);
        Assert.Equal("Check doesn't compile: line 3, column 5: CS1002 ; expected.", failure.Sentence);
        Assert.Equal("Open Check", failure.FixLabel);
    }

    [Fact]
    public void SND_CODE_002_says_the_step_ran_past_its_time_limit_and_keeps_the_limit_and_output_in_the_detail()
    {
        const string Error = "SND-CODE-002: the code ran past its limit of 30 s: started";

        var failure = StepFailures.Describe("Check", Error);

        Assert.NotNull(failure);
        Assert.Equal("Check ran past its time limit.", failure.Sentence);
        Assert.Equal("Open Check", failure.FixLabel);
        Assert.Equal(Error, failure.Detail);
    }

    [Fact]
    public void SND_CODE_003_says_the_step_used_more_memory_than_its_limit()
    {
        var failure = StepFailures.Describe("Check", "SND-CODE-003: the code ran out of memory");

        Assert.NotNull(failure);
        Assert.Equal("Check used more memory than its limit.", failure.Sentence);
        Assert.Equal("Open Check", failure.FixLabel);
    }

    [Fact]
    public void SND_CODE_005_with_the_runner_missing_says_it_is_not_installed_and_offers_the_install_command()
    {
        var failure = StepFailures.Describe("Measure", "SND-CODE-005: Runner binary not found at /home/me/.agentic-workflow/bin/cs/current/sindri-code-runner. Install it beside the Sindri host.");

        Assert.NotNull(failure);
        Assert.Equal("SND-CODE-005", failure.Code);
        Assert.Equal("Sindri can't run code steps: sindri-code-runner isn't installed.", failure.Sentence);
        Assert.Equal("Install the code runner", failure.FixLabel);
        Assert.Equal("scripts/install-cs.sh", failure.FixCommand);
        Assert.Contains("/home/me/.agentic-workflow/bin/cs/current/sindri-code-runner", failure.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void SND_CODE_005_for_another_cause_says_the_runner_could_not_start_shows_why_and_offers_no_install()
    {
        var failure = StepFailures.Describe("Measure", "SND-CODE-005: the runner did not start");

        Assert.NotNull(failure);
        Assert.Equal("Sindri can't run code steps: the code runner couldn't start.\nthe runner did not start", failure.Sentence);
        Assert.Null(failure.FixLabel);
        Assert.Null(failure.FixCommand);
    }

    // why: until #3757 lands the engine still writes the older text for a missing runner; both read as "not installed"
    [Fact]
    public void SND_CODE_005_with_the_older_missing_runner_text_says_it_is_not_installed_too()
        => Assert.Equal(
            "Sindri can't run code steps: sindri-code-runner isn't installed.",
            StepFailures.Describe("Measure", "SND-CODE-005: the runner binary is not at /x/sindri-code-runner (fix: install it beside the Sindri host)")?.Sentence);

    [Fact]
    public void SND_CODE_005_when_the_runner_is_there_but_cannot_start_offers_no_install()
    {
        var failure = StepFailures.Describe("Measure", "SND-CODE-005: Runner binary at /x/sindri-code-runner could not start: Permission denied");

        Assert.NotNull(failure);
        Assert.Equal("Sindri can't run code steps: the code runner couldn't start.\nRunner binary at /x/sindri-code-runner could not start: Permission denied", failure.Sentence);
        Assert.Null(failure.FixCommand);
    }

    [Theory]
    [InlineData("SND-CODE-001: line 1, column 1: CS0103 x")]
    [InlineData("SND-CODE-002: the code ran past its limit of 30 s")]
    [InlineData("SND-CODE-003: the code ran out of memory")]
    [InlineData("SND-CODE-004: InvalidOperationException: boom")]
    public void A_code_step_sentence_names_the_step_by_its_title_never_a_node_id(string error)
    {
        var failure = StepFailures.Describe("Nightly check", error);

        Assert.NotNull(failure);
        Assert.StartsWith("Nightly check ", failure.Sentence, StringComparison.Ordinal);
        Assert.DoesNotContain("node '", failure.Sentence, StringComparison.Ordinal);
        Assert.DoesNotContain("SND-CODE", failure.Sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void A_prompt_step_quotes_the_raw_answer_and_names_the_step_in_its_second_button()
    {
        var failure = StepFailures.Describe("Plan", "SND-GRAPH-103: the model's answer is not JSON", raw: "Usage limit reached");

        Assert.NotNull(failure);
        Assert.Equal("The model answered with text, not the JSON this step asks for: \"Usage limit reached\".", failure.Sentence);
        Assert.Equal("Retry with model", failure.FixLabel);
        Assert.Equal("Open Plan", failure.SecondaryLabel);
    }

    [Fact]
    public void A_row_that_needs_a_value_the_error_does_not_carry_gives_no_sentence()
    {
        Assert.Null(StepFailures.Describe("Plan", "SND-GRAPH-103: the model's answer is not JSON"));
        Assert.Null(StepFailures.Describe("Review", "SND-GRAPH-113: a field came out empty"));
    }

    [Theory]
    [InlineData("SND-NOPE-999: something")]
    [InlineData("boom")]
    [InlineData("")]
    [InlineData("node 'check' failed: SND-CODE-004: boom")]
    public void An_error_with_no_row_gives_no_sentence(string error)
        => Assert.Null(StepFailures.Describe("Check", error));
}
