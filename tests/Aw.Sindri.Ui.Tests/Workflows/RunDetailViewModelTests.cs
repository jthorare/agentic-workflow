using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
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

        Assert.Equal(("Run morning-check@1:1", "✓ Succeeded", "Started by manual", "Revision 1"), (run.TitleText, run.ChipGlyph + " " + run.ChipWord, run.TriggerText, run.RevisionText));
        Assert.Contains("\"summary\": \"no drift\"", run.OutputsText, StringComparison.Ordinal);
        Assert.Equal((true, false), (run.HasOutputs, run.HasError));
    }

    [Fact]
    public async Task A_finished_run_lists_each_node_with_its_input_and_output()
    {
        var run = await _f.OpenedRun(Token);

        Assert.Equal(Served(Golden("workflow-runs-run", "finished")), run.Groups.Select(g => g.Node), StringComparer.Ordinal);   // the server's order (graph order, #3307)
        var agent = Assert.Single(Group(run, "agent").Runs);
        Assert.Equal(("✓ Succeeded", true, true), (agent.ChipGlyph + " " + agent.ChipWord, agent.HasInput, agent.HasOutput));
        Assert.Contains("\"prompt\": \"Look for drift\"", agent.InputText, StringComparison.Ordinal);
        Assert.Contains("\"text\": \"no drift\"", agent.OutputText, StringComparison.Ordinal);
        Assert.False(Group(run, "agent").IsForEach);
    }

    [Fact]
    public async Task A_node_with_no_input_shows_none()
    {
        var run = await _f.OpenedRun(Token);

        var start = Assert.Single(Group(run, "start").Runs);
        Assert.Equal((false, string.Empty), (start.HasInput, start.InputText));
    }

    [Fact]
    public async Task A_running_run_shows_a_running_node_with_no_output_yet()
    {
        _f.ServeRun("running");

        var run = await _f.OpenedRun(Token);

        var agent = Assert.Single(Group(run, "agent").Runs);
        Assert.Equal(("● Running", "● Running", false, false), (run.ChipGlyph + " " + run.ChipWord, agent.ChipGlyph + " " + agent.ChipWord, agent.HasOutput, run.HasOutputs));
        Assert.Equal("Started 4m ago", run.WhenText);
    }

    [Fact]
    public async Task A_node_with_truncated_output_says_so()
    {
        var served = Golden("workflow-runs-run", "finished");
        Node(served, "agent")["truncated"] = true;
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        var (cut, whole) = (Group(run, "agent").Runs[0], Group(run, "start").Runs[0]);
        Assert.Equal((true, "Cut to fit Sindri's size limit."), (cut.IsTruncated, cut.TruncatedText));
        Assert.False(whole.IsTruncated);
    }

    [Fact]
    public async Task A_for_each_node_shows_its_items_grouped_under_the_node()
    {
        var served = Golden("workflow-runs-run", "finished");
        var nodes = served["nodes"]!.AsArray();
        var agent = Node(served, "agent").ToJsonString();   // read once: after the first Add there are two "agent" rows and Single would throw
        foreach (var index in new[] { 1, 2 })
        {
            var next = JsonNode.Parse(agent)!;
            next["itemIndex"] = index;
            nodes.Add(next);
        }

        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        var group = Group(run, "agent");
        Assert.Equal((2, "agent · 3 items", true), (run.Groups.Count, group.HeaderText, group.IsForEach));
        Assert.Equal(["Item 1 of 3", "Item 2 of 3", "Item 3 of 3"], group.Runs.Select(r => r.TitleText), StringComparer.Ordinal);
        _ = Assert.Single(Group(run, "start").Runs);
    }

    [Fact]
    public async Task A_failed_node_shows_its_error_and_the_run_shows_its_own()
    {
        var served = Golden("workflow-runs-run", "finished");
        served["status"] = "failed";
        served["error"] = "node 'agent' failed";
        Node(served, "agent")["status"] = "failed";
        Node(served, "agent")["error"] = "boom";
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        var agent = Group(run, "agent").Runs[0];
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
        Node(served, "agent")["status"] = status;
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        var agent = Group(run, "agent").Runs[0];
        Assert.Equal(expected, agent.ChipGlyph + " " + agent.ChipWord);
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

    // #3307: a plain node's block read "check", then "check ✕ Failed" under it. The heading names the node; its one row shows only the status.
    [Fact]
    public async Task A_node_that_runs_once_is_named_once_by_its_heading()
    {
        var run = await _f.OpenedRun(Token);

        var group = Group(run, "agent");
        var row = Assert.Single(group.Runs);
        Assert.Equal(("agent", string.Empty, false), (group.HeaderText, row.TitleText, row.HasTitle));
        Assert.Equal("agent, succeeded", row.Name);   // the screen reader still hears which node
    }

    [Fact]
    public async Task Each_item_of_a_for_each_node_keeps_its_own_title()
    {
        var served = Golden("workflow-runs-run", "finished");
        var next = JsonNode.Parse(Node(served, "agent").ToJsonString())!;
        next["itemIndex"] = 1;
        served["nodes"]!.AsArray().Add(next);
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        Assert.All(Group(run, "agent").Runs, static r => Assert.True(r.HasTitle));
    }

    // #3307: a code step's Input showed its body as one escaped JSON string ("var mb = input!.GetValue<int>(); … $\"repo is {mb} MB…\"").
    [Fact]
    public async Task A_code_steps_body_shows_as_code_and_the_rest_of_its_input_as_json()
    {
        var served = Golden("workflow-runs-run", "finished");
        Node(served, "agent")["input"] = new JsonObject
        {
            ["input"] = 120,
            ["code"] = "var mb = input!.GetValue<int>();\nreturn $\"repo is {mb} MB\";",
            ["timeout"] = "60s",
        };
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        var row = Group(run, "agent").Runs[0];
        Assert.Equal((true, "Code"), (row.HasCode, row.CodeHeading));
        Assert.Equal("var mb = input!.GetValue<int>();\nreturn $\"repo is {mb} MB\";", row.CodeText);
        Assert.DoesNotContain("\"code\"", row.InputText, StringComparison.Ordinal);
        Assert.Equal("{\n  \"input\": 120,\n  \"timeout\": \"60s\"\n}", row.InputText);
    }

    [Fact]
    public async Task A_code_step_with_only_its_body_shows_no_input_beside_the_code()
    {
        var served = Golden("workflow-runs-run", "finished");
        Node(served, "agent")["input"] = new JsonObject { ["code"] = "return 1;" };
        _f.ServeRun(served);

        var run = await _f.OpenedRun(Token);

        var row = Group(run, "agent").Runs[0];
        Assert.Equal((true, "return 1;", false, string.Empty), (row.HasCode, row.CodeText, row.HasInput, row.InputText));
    }

    [Fact]
    public async Task A_step_with_no_code_in_its_input_shows_no_code()
    {
        var run = await _f.OpenedRun(Token);

        var row = Group(run, "agent").Runs[0];
        Assert.Equal((false, string.Empty), (row.HasCode, row.CodeText));
    }

    private static NodeGroupViewModel Group(RunDetailViewModel run, string node) => run.Groups.Single(g => g.Node == node);

    private static JsonNode Node(JsonNode served, string node) => served["nodes"]!.AsArray().Single(n => n!["node"]!.GetValue<string>() == node)!;

    private static string[] Served(JsonNode served) => [.. served["nodes"]!.AsArray().Select(static n => n!["node"]!.GetValue<string>()).Distinct(StringComparer.Ordinal)];
}
