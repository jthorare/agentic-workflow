using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflows;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3304 (spec §7.3): a failed run's page says "Failed at {step}" and Sindri's sentence, offers the fix (a command fix copies the command), and keeps the engine's text under Details;
/// the runs list shows the sentence, never "node 'check' failed: SND-...".
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RunFailureTests
{
    private const string EngineError = "node 'agent' failed: SND-LAUNCH-003: the agent target 'claude-code' is not installed";

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_failed_run_says_where_it_failed_and_why_in_words()
    {
        _f.ServeRun("failed");

        var run = await _f.OpenedRun(Token);

        var failure = run.Failure;
        Assert.NotNull(failure);
        Assert.Equal("Failed at Check the repos", failure.HeadingText);
        Assert.Equal("The agent for step Check the repos isn't available: its CLI is not installed or the target is not in the catalog.", failure.SentenceText);
        Assert.True(run.HasFailure);
    }

    [Fact]
    public async Task The_engine_text_moves_under_details_and_leaves_the_header()
    {
        _f.ServeRun("failed");

        var run = await _f.OpenedRun(Token);

        Assert.False(run.HasError);
        Assert.Equal(("Details", EngineError), (run.Failure!.DetailsHeading, run.Failure.DetailText));
    }

    [Fact]
    public async Task A_command_fix_copies_the_command_and_says_copied()
    {
        _f.ServeRun("failed");
        var run = await _f.OpenedRun(Token);
        var failure = run.Failure!;

        Assert.True(failure.HasCopyFix);
        Assert.Equal("Choose an agent", failure.Fix!.Label);
        await failure.CopyFix(Token);

        Assert.Equal("sindri profile set workflow.agent.target <id>", _f.Clipboard.Text);
        Assert.Equal("Copied", failure.Fix.Label);
    }

    [Fact]
    public async Task A_fix_with_no_command_shows_no_copy_button()
    {
        var served = Golden("workflow-runs-run", "failed");
        AgentNode(served)["failure"]!["fixCommand"] = null;
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        Assert.False(run.Failure!.HasCopyFix);
    }

    [Fact]
    public async Task A_failed_run_whose_node_carries_no_failure_keeps_the_engine_text_in_the_header()
    {
        var served = Golden("workflow-runs-run", "failed");
        _ = AgentNode(served).Remove("failure");
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        Assert.Null(run.Failure);
        Assert.False(run.HasFailure);
        Assert.Equal((true, EngineError), (run.HasError, run.ErrorText));
    }

    [Fact]
    public async Task A_failed_node_row_shows_the_sentence_not_the_code()
    {
        _f.ServeRun("failed");

        var run = await _f.OpenedRun(Token);

        var agent = Assert.Single(run.Groups.Single(g => g.Node == "agent").Runs);
        Assert.StartsWith("The agent for step Check the repos", agent.ErrorText, StringComparison.Ordinal);
        Assert.DoesNotContain("SND-", agent.ErrorText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_run_row_shows_its_sentence_instead_of_the_engine_text()
    {
        var served = Golden("workflows-slug-runs", "list-three");
        served["runs"]![1]!["error"] = "node 'check' failed: SND-CODE-004: InvalidOperationException: repo is 812 MB";
        served["runs"]![1]!["failureSentence"] = "Check stopped: repo is 812 MB.";
        _f.Server.WorkflowRunsAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, served.ToJsonString());
        var page = await _f.Shown(Token);

        await page.OpenWorkflow(Slug, Token);

        var failed = page.Runs.Rows[1];
        Assert.Equal(("Check stopped: repo is 812 MB.", true), (failed.ErrorText, failed.HasError));
        Assert.DoesNotContain("node '", failed.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_fix_with_only_a_label_is_a_button_that_opens_the_failed_step()
    {
        var served = Golden("workflow-runs-run", "failed");
        AgentNode(served)["failure"]!["fixCommand"] = null;   // SND-CODE-004's "Open Check": a label, no command
        _f.ServeRun(served);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.OpenRun(RunId, Token);

        var failure = page.Run.Failure!;
        Assert.Equal((true, false, "Choose an agent"), (failure.HasOpenFix, failure.HasCopyFix, failure.OpenText));

        await page.OpenFailedStep(Token);

        Assert.True(page.IsRuns);   // back on the workflow, where the graph is
        Assert.Equal("agent", page.Runs.Graph!.SelectedId);
    }

    [Fact]
    public async Task A_command_fix_is_a_copy_button_and_not_an_open_button()
    {
        _f.ServeRun("failed");

        var run = await _f.OpenedRun(Token);

        Assert.Equal((true, false), (run.Failure!.HasCopyFix, run.Failure.HasOpenFix));
    }

    [Fact]
    public async Task A_run_that_succeeded_shows_no_failure_callout_even_when_a_continued_step_failed()
    {
        var served = Golden("workflow-runs-run", "finished");
        var continued = Golden("workflow-runs-run", "failed")["nodes"]!.AsArray().Single(n => (string?)n!["node"] == "agent")!.DeepClone();   // a step with onFailure 'continue' that failed: its row keeps a failure
        served["nodes"]!.AsArray().Add(continued);
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        Assert.Equal((false, null), (run.HasFailure, run.Failure));
    }

    [Fact]
    public async Task A_failed_run_names_the_step_its_error_names_not_another_step_that_failed_and_continued()
    {
        var served = Golden("workflow-runs-run", "failed");   // the run's error names 'agent'
        var other = AgentNode(served).DeepClone();
        other["node"] = "later";
        other["failure"]!["step"] = "Later step";
        served["nodes"]!.AsArray().Add(other);   // listed after 'agent', failed too, and continued
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        Assert.Equal("Failed at Check the repos", run.Failure!.HeadingText);
        Assert.Equal("agent", run.Failure.Node);
    }

    private RunRowViewModel FailedRow(string? sentence) =>
        new(new WorkflowRunRowView("feed-check@1:1", 1, "failed", "manual", Now.AddMinutes(-2), Now.AddMinutes(-1), "node 'check' failed: SND-CODE-004: InvalidOperationException: the feed is down",
            Seconds: 1, Result: "node 'check' failed: SND-CODE-004: InvalidOperationException: the feed is down", FailureSentence: sentence), _f.Clock);

    [Fact]
    public void A_failed_rows_result_cell_reads_the_sentence_not_the_engine_text()
    {
        var row = FailedRow("Check stopped: the feed is down.");

        Assert.Equal(("Check stopped: the feed is down.", "Check stopped: the feed is down."), (row.ResultText, row.ErrorText));
    }

    [Fact]
    public void A_failed_row_with_no_sentence_keeps_the_raw_result()
    {
        var row = FailedRow(sentence: null);

        Assert.Equal("node 'check' failed: SND-CODE-004: InvalidOperationException: the feed is down", row.ResultText);
    }

    // The run answer lists nodes in step order, so the failed agent node is found by name, not by index.
    private static JsonObject AgentNode(JsonNode served) =>
        served["nodes"]!.AsArray().Single(n => (string?)n!["node"] == "agent")!.AsObject();
}
