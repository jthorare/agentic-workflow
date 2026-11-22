using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Opening a failed live run marks it seen once, so its Home row clears (#3753); a read alone sends no write.</summary>
[Lane(TestLane.Unit)]
public sealed class RunDetailSeenTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Opening_a_failed_run_sends_the_seen_mark_once()
    {
        ServeFailed();

        await _f.OpenedRun(Token);

        Assert.Equal([RunId], _f.Server.SeenWorkflowRuns, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Opening_a_succeeded_run_sends_no_seen_mark()
    {
        var run = await _f.OpenedRun(Token);

        Assert.Equal("✓ Succeeded", run.ChipGlyph + " " + run.ChipWord);
        Assert.Empty(_f.Server.SeenWorkflowRuns);
    }

    [Fact]
    public async Task Reading_the_open_failed_run_again_sends_no_second_seen_mark()
    {
        ServeFailed();
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.OpenRun(RunId, Token);
        var reads = _f.Server.WorkflowRunCalls;

        await page.Show(Url, Token);   // the page is shown again: it reads the open run again
        await page.RetryRun(Token);

        Assert.Equal(reads + 2, _f.Server.WorkflowRunCalls);
        Assert.Equal([RunId], _f.Server.SeenWorkflowRuns, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_refused_seen_mark_still_shows_the_run()
    {
        ServeFailed();
        _f.Server.MarkWorkflowRunSeenAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-GRAPH-101", "the mark failed");

        var run = await _f.OpenedRun(Token);

        Assert.Equal([RunId], _f.Server.SeenWorkflowRuns, StringComparer.Ordinal);
        Assert.Equal(("✕ Failed", "node 'agent' failed", false, false), (run.ChipGlyph + " " + run.ChipWord, run.ErrorText, run.IsFailed, run.HasNotice));
    }

    private void ServeFailed()
    {
        var served = Golden("workflow-runs-run", "finished");
        served["status"] = "failed";
        served["error"] = "node 'agent' failed";
        _f.ServeRun(served);
    }
}
