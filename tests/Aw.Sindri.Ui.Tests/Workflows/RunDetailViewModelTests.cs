using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>A run's detail (#2258 PR 17 part 1): every node with status as glyph and word, input, output, the cut-short note, the error, and a for_each node's items grouped under it.</summary>
[Lane(TestLane.Unit)]
public sealed class RunDetailViewModelTests
{
    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_finished_run_shows_its_status_trigger_and_outputs()
    {
        var run = await _f.OpenedRun(Token);

        Assert.Equal(("✓ Succeeded", "Started by manual", "Revision 1"), (run.ChipGlyph + " " + run.ChipWord, run.TriggerText, run.RevisionText));
        Assert.Contains("\"summary\": \"no drift\"", run.OutputsText, StringComparison.Ordinal);
        Assert.Equal((true, false), (run.HasOutputs, run.HasError));
    }

    [Fact]
    public async Task A_finished_run_lists_each_node_with_its_input_and_output()
    {
        var run = await _f.OpenedRun(Token);

        Assert.Equal(["agent", "start"], run.Groups.Select(g => g.Node), StringComparer.Ordinal);
        var agent = Assert.Single(run.Groups[0].Runs);
        Assert.Equal(("agent", "✓ Succeeded", true, true), (agent.TitleText, agent.ChipGlyph + " " + agent.ChipWord, agent.HasInput, agent.HasOutput));
        Assert.Contains("\"prompt\": \"Look for drift\"", agent.InputText, StringComparison.Ordinal);
        Assert.Contains("\"text\": \"no drift\"", agent.OutputText, StringComparison.Ordinal);
        Assert.False(run.Groups[0].IsForEach);
    }

    [Fact]
    public async Task A_node_with_no_input_shows_none()
    {
        var run = await _f.OpenedRun(Token);

        var start = Assert.Single(run.Groups[1].Runs);
        Assert.Equal((false, string.Empty), (start.HasInput, start.InputText));
    }

    [Fact]
    public async Task A_running_run_shows_a_running_node_with_no_output_yet()
    {
        _f.ServeRun("running");

        var run = await _f.OpenedRun(Token);

        var agent = Assert.Single(run.Groups[0].Runs);
        Assert.Equal(("● Running", "● Running", false, false), (run.ChipGlyph + " " + run.ChipWord, agent.ChipGlyph + " " + agent.ChipWord, agent.HasOutput, run.HasOutputs));
        Assert.Equal("Started 4m ago", run.WhenText);
    }

    [Fact]
    public async Task A_node_with_truncated_output_says_so()
    {
        var served = Golden("workflow-runs-run", "finished");
        served["nodes"]![0]!["truncated"] = true;
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        var (cut, whole) = (run.Groups[0].Runs[0], run.Groups[1].Runs[0]);
        Assert.Equal((true, "Cut to fit Sindri's size limit."), (cut.IsTruncated, cut.TruncatedText));
        Assert.False(whole.IsTruncated);
    }

    [Fact]
    public async Task A_for_each_node_shows_its_items_grouped_under_the_node()
    {
        var served = Golden("workflow-runs-run", "finished");
        var nodes = served["nodes"]!.AsArray();
        foreach (var index in new[] { 1, 2 })
        {
            var next = JsonNode.Parse(nodes[0]!.ToJsonString())!;
            next["itemIndex"] = index;
            nodes.Add(next);
        }

        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        var group = run.Groups[0];
        Assert.Equal((2, "agent · 3 items", true), (run.Groups.Count, group.HeaderText, group.IsForEach));
        Assert.Equal(["Item 1 of 3", "Item 2 of 3", "Item 3 of 3"], group.Runs.Select(r => r.TitleText), StringComparer.Ordinal);
        Assert.Equal("start", run.Groups[1].Node);
    }

    [Fact]
    public async Task A_failed_node_shows_its_error_and_the_run_shows_its_own()
    {
        var served = Golden("workflow-runs-run", "finished");
        served["status"] = "failed";
        served["error"] = "node 'agent' failed";
        served["nodes"]![0]!["status"] = "failed";
        served["nodes"]![0]!["error"] = "boom";
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        var agent = run.Groups[0].Runs[0];
        Assert.Equal(("✕ Failed", "boom", true), (agent.ChipGlyph + " " + agent.ChipWord, agent.ErrorText, agent.HasError));
        Assert.Equal(("✕ Failed", "node 'agent' failed", true), (run.ChipGlyph + " " + run.ChipWord, run.ErrorText, run.HasError));
        Assert.Equal("agent, failed, boom", agent.Name);
    }

    [Theory]
    [InlineData("queued", "◌ Queued")]
    [InlineData("waiting", "◐ Waiting")]
    [InlineData("skipped", "– Skipped")]
    [InlineData("cancelled", "○ Cancelled")]
    [InlineData("paused", "○ Paused")]
    public async Task Every_status_has_a_glyph_and_a_word_and_one_this_build_does_not_know_renders_by_its_served_word(string status, string expected)
    {
        var served = Golden("workflow-runs-run", "finished");
        served["nodes"]![0]!["status"] = status;
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        Assert.Equal(expected, run.Groups[0].Runs[0].ChipGlyph + " " + run.Groups[0].Runs[0].ChipWord);
    }

    [Fact]
    public async Task An_unknown_run_is_a_failed_read_with_the_served_reason()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);

        await page.OpenRun("morning-check@1:9", Token);

        Assert.True(page.Run.IsFailed);
        Assert.StartsWith("Couldn't read the run just now.", page.Run.FailedText, StringComparison.Ordinal);
        Assert.Contains("No run 'morning-check@1:9'", page.Run.FailedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Opening_a_run_after_a_failed_one_does_not_show_the_old_failure_while_reading()
    {
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.OpenRun("morning-check@1:9", Token);
        Assert.True(page.Run.IsFailed);
        var gate = new TaskCompletionSource();
        _f.Server.WorkflowRunGates[RunId] = gate;

        var opening = page.OpenRun(RunId, Token);

        Assert.Equal((false, string.Empty, true), (page.Run.IsFailed, page.Run.FailedText, page.Run.IsReading));
        gate.SetResult();
        await opening;
        Assert.Equal((false, "✓ Succeeded"), (page.Run.IsFailed, page.Run.ChipGlyph + " " + page.Run.ChipWord));
    }
}
