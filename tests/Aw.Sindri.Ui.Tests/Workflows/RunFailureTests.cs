using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
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
        served["nodes"]![0]!["failure"]!["fixCommand"] = null;
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        Assert.False(run.Failure!.HasCopyFix);
    }

    [Fact]
    public async Task A_failed_run_whose_node_carries_no_failure_keeps_the_engine_text_in_the_header()
    {
        var served = Golden("workflow-runs-run", "failed");
        _ = served["nodes"]![0]!.AsObject().Remove("failure");
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

        var agent = Assert.Single(run.Groups[0].Runs);
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
}
