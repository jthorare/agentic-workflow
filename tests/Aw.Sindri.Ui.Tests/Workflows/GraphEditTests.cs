using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The graph edits on the Workflows page (#2258 PR 17 part 3b): Add step, Remove and Connect on the selected node, sent on the revision read; commands off when nothing can be edited.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphEditTests
{
    // 'found' is at revision 2 with nodes start (manual) and agent (run_agent, "Check the repos")
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Blocks = """
        {"groups":[
          {"group":"Triggers","blocks":[{"type":"manual","title":"Manual","isTrigger":true,"config":[],"outputs":{},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]},
          {"group":"AI","blocks":[{"type":"run_agent","title":"Run an agent","isTrigger":false,"config":[{"name":"prompt","type":"text","required":true}],"outputs":{"text":"text"},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]}
        ]}
        """;
#pragma warning restore MA0136

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> Open(string? selected)
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        if (selected is not null)
        {
            Assert.True(page.Runs.Graph!.SelectNode(selected));
        }

        return page;
    }

    private void AssertSent(int index, string ops)
    {
        var sent = _f.Server.AppliedWorkflowOps[index];
        Assert.Equal((Slug, 2), (sent.Slug, sent.BaseRevision));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(ops), JsonNode.Parse(sent.Ops)), sent.Ops);
    }

    [Fact]
    public async Task Adding_a_step_sends_add_node__and_connect_after_the_selected_node_then_reads_the_workflow_again()
    {
        var page = await Open("start");
        await page.GraphEdit.BeginAdd(Token);
        Assert.True(page.GraphEdit.IsAdding);
        Assert.Equal(["Triggers", "AI"], page.GraphEdit.Groups.Select(g => g.Group), StringComparer.Ordinal);
        var runs = _f.Server.WorkflowRunsCalls;

        await page.GraphEdit.AddStep(page.GraphEdit.Groups[1].Blocks[0], Token);

        _ = Assert.Single(_f.Server.AppliedWorkflowOps);
        AssertSent(0, """[{"op":"add_node","id":"run_agent_1","type":"run_agent"},{"op":"connect","from":"start","to":"run_agent_1"}]""");
        Assert.Equal(runs + 1, _f.Server.WorkflowRunsCalls);
        Assert.False(page.GraphEdit.IsAdding);
    }

    [Fact]
    public async Task Adding_a_step_with_nothing_selected_sends_only_add_node_and_a_taken_id_is_skipped()
    {
        var page = await Open(selected: null);
        await page.GraphEdit.BeginAdd(Token);

        await page.GraphEdit.AddStep(page.GraphEdit.Groups[0].Blocks[0], Token);   // manual: the spec has "start" (manual) but not "manual_1"
        await page.GraphEdit.AddStep(page.GraphEdit.Groups[0].Blocks[0], Token);

        AssertSent(0, """[{"op":"add_node","id":"manual_1","type":"manual"}]""");
        Assert.Equal(2, _f.Server.AppliedWorkflowOps.Count);
    }

    [Fact]
    public async Task Removing_asks_first_naming_the_node_then_sends_remove_node()
    {
        var page = await Open("agent");

        page.GraphEdit.RequestRemove();

        Assert.True(page.GraphEdit.IsConfirmingRemove);
        Assert.Contains("Check the repos", page.GraphEdit.ConfirmText, StringComparison.Ordinal);
        Assert.Empty(_f.Server.AppliedWorkflowOps);

        page.GraphEdit.Cancel();
        Assert.Equal((false, 0), (page.GraphEdit.IsConfirmingRemove, _f.Server.AppliedWorkflowOps.Count));

        page.GraphEdit.RequestRemove();
        await page.GraphEdit.ConfirmRemove(Token);

        AssertSent(0, """[{"op":"remove_node","node":"agent"}]""");
        Assert.False(page.GraphEdit.IsConfirmingRemove);
    }

    [Fact]
    public async Task The_delete_key_on_the_canvas_asks_to_remove_the_selected_node()
    {
        var page = await Open("agent");

        _ = page.Runs.Graph!.RequestRemoveSelected();

        Assert.True(page.GraphEdit.IsConfirmingRemove);
    }

    [Fact]
    public async Task Connecting_lists_the_other_nodes_and_sends_connect_from_the_selected_one()
    {
        var page = await Open("start");

        page.GraphEdit.BeginConnect();
        Assert.True(page.GraphEdit.IsConnecting);
        Assert.Equal(["agent"], page.GraphEdit.Targets.Select(t => t.Id), StringComparer.Ordinal);

        await page.GraphEdit.ConnectTo("agent", Token);

        AssertSent(0, """[{"op":"connect","from":"start","to":"agent"}]""");
        Assert.False(page.GraphEdit.IsConnecting);
    }

    [Fact]
    public async Task A_stale_answer_shows_the_reload_prompt_turns_edits_off_and_reload_brings_them_back()
    {
        var page = await Open("start");
        await page.GraphEdit.BeginAdd(Token);
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem("workflows-slug-ops", "ops-stale");

        await page.GraphEdit.AddStep(page.GraphEdit.Groups[1].Blocks[0], Token);

        Assert.StartsWith("This workflow changed elsewhere", page.GraphEdit.ReloadPromptText, StringComparison.Ordinal);
        Assert.Equal((false, false, false), (page.GraphEdit.CanAdd, page.GraphEdit.CanRemove, page.GraphEdit.CanConnect));

        await page.GraphEdit.Reload(Token);

        Assert.Equal((string.Empty, true), (page.GraphEdit.ReloadPromptText, page.GraphEdit.CanAdd));
    }

    [Fact]
    public async Task Every_command_is_off_with_no_workflow_open_and_for_an_archived_one()
    {
        var closed = await _f.Shown(Token);
        Assert.Equal((false, false, false), (closed.GraphEdit.CanAdd, closed.GraphEdit.CanRemove, closed.GraphEdit.CanConnect));

        var archived = Golden("workflows-slug", "found");
        archived["archivedAt"] = "2026-01-01T00:00:00Z";
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, archived.ToJsonString());
        var page = await Open("start");

        Assert.Equal((false, false, false), (page.GraphEdit.CanAdd, page.GraphEdit.CanRemove, page.GraphEdit.CanConnect));
    }

    [Fact]
    public async Task On_a_live_workflow_the_commands_are_on_and_remove_and_connect_need_a_selected_node()
    {
        var page = await Open(selected: null);
        Assert.Equal((true, false, false), (page.GraphEdit.CanAdd, page.GraphEdit.CanRemove, page.GraphEdit.CanConnect));

        _ = page.Runs.Graph!.SelectNode("start");

        Assert.Equal((true, true, true), (page.GraphEdit.CanAdd, page.GraphEdit.CanRemove, page.GraphEdit.CanConnect));
    }

    [Fact]
    public async Task An_edit_on_a_revision_the_page_never_showed_sends_nothing_and_asks_to_reload()
    {
        var page = await Open("agent");
        var newer = Golden("workflows-slug", "found");
        newer["latestRevision"] = 3;
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, newer.ToJsonString());   // saved elsewhere after the page read revision 2

        page.GraphEdit.RequestRemove();
        await page.GraphEdit.ConfirmRemove(Token);

        Assert.Empty(_f.Server.AppliedWorkflowOps);
        Assert.True(page.GraphEdit.HasReloadPrompt);
    }

    [Fact]
    public async Task The_block_picker_closes_when_another_workflow_opens()
    {
        const string other = "evening-check";
        _f.Server.WorkflowAnswers[other] = RouteAnswer.Api("workflows-slug", "found");
        var page = await Open("start");
        await page.GraphEdit.BeginAdd(Token);
        Assert.True(page.GraphEdit.IsAdding);

        page.Back();
        await page.OpenWorkflow(other, Token);

        Assert.False(page.GraphEdit.IsAdding);
    }
}
