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
          {"group":"AI","blocks":[{"type":"run_agent","title":"Run an agent","isTrigger":false,"config":[{"name":"prompt","type":"text","required":true}],"outputs":{"text":"text"},"branches":["out"],"exampleConfig":{"prompt":"Summarize the open pull requests"},"exampleSpec":null},
            {"type":"ask_owner","title":"Ask owner","isTrigger":false,"config":[{"name":"question","type":"text","required":true}],"outputs":{},"branches":["out"],"exampleConfig":null,"exampleSpec":null},
            {"type":"fetch","title":"Fetch","isTrigger":false,"config":[{"name":"note","type":"text","required":false},{"name":"url","type":"text","required":true}],"outputs":{},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]}
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

    // Add step on the run_agent block, then Save with its example untouched: the one add_node goes out here.
    private static async Task AddAndSave(WorkflowsPageViewModel page)
    {
        await page.GraphEdit.AddStep(page.GraphEdit.Groups[1].Blocks[0], Token);
        await page.Save(Token);
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

        await AddAndSave(page);

        _ = Assert.Single(_f.Server.AppliedWorkflowOps);
        AssertSent(0, """[{"op":"add_node","id":"run_agent_1","type":"run_agent","config":{"prompt":"Summarize the open pull requests"}},{"op":"connect","from":"start","to":"run_agent_1"}]""");
        Assert.Equal(runs + 1, _f.Server.WorkflowRunsCalls);
        Assert.False(page.GraphEdit.IsAdding);
    }

    [Fact]
    public async Task Adding_a_step_with_nothing_selected_sends_only_add_node_and_a_taken_id_is_skipped()
    {
        var page = await Open(selected: null);
        await page.GraphEdit.BeginAdd(Token);

        await page.GraphEdit.AddStep(page.GraphEdit.Groups[0].Blocks[0], Token);   // manual: the spec has "start" (manual) but not "manual_1"
        await page.Save(Token);
        await page.GraphEdit.BeginAdd(Token);
        await page.GraphEdit.AddStep(page.GraphEdit.Groups[0].Blocks[0], Token);
        await page.Save(Token);

        AssertSent(0, """[{"op":"add_node","id":"manual_1","type":"manual"}]""");
        Assert.Equal(2, _f.Server.AppliedWorkflowOps.Count);
    }

    [Fact]
    public async Task Add_step_opens_the_sheet_prefilled_with_the_blocks_example()
    {
        var page = await Open("start");
        await page.GraphEdit.BeginAdd(Token);
        var nodes = page.Runs.Graph!.Nodes.Count;

        await page.GraphEdit.AddStep(page.GraphEdit.Groups[1].Blocks[0], Token);

        Assert.Empty(_f.Server.AppliedWorkflowOps);
        Assert.True(page.IsEditing);
        Assert.True(page.IsNewStep);
        Assert.Equal([("prompt", "Summarize the open pull requests")], page.Editor.Fields.Select(f => (f.Name, f.Text)));
        Assert.True(page.CanSave);   // the example is complete, so Save works untouched
        Assert.Equal(nodes, page.Runs.Graph.Nodes.Count);
    }

    [Fact]
    public async Task Saving_a_new_step_sends_one_add_node_with_its_settings()
    {
        var page = await Open("start");
        await page.GraphEdit.BeginAdd(Token);
        await page.GraphEdit.AddStep(page.GraphEdit.Groups[1].Blocks[0], Token);
        page.Editor.Fields[0].Text = "Check the repos";

        await page.Save(Token);

        _ = Assert.Single(_f.Server.AppliedWorkflowOps);
        AssertSent(0, """[{"op":"add_node","id":"run_agent_1","type":"run_agent","config":{"prompt":"Check the repos"}},{"op":"connect","from":"start","to":"run_agent_1"}]""");
        Assert.False(page.IsNewStep);
        Assert.False(page.IsEditing);
    }

    [Fact]
    public async Task Cancel_on_a_new_step_writes_nothing()
    {
        var page = await Open("start");
        await page.GraphEdit.BeginAdd(Token);
        var nodes = page.Runs.Graph!.Nodes.Count;
        await page.GraphEdit.AddStep(page.GraphEdit.Groups[1].Blocks[0], Token);
        page.Editor.Fields[0].Text = "half typed";

        page.CloseEditor();

        Assert.Empty(_f.Server.AppliedWorkflowOps);
        Assert.False(page.IsNewStep);
        Assert.False(page.IsEditing);
        Assert.Equal(nodes, page.Runs.Graph.Nodes.Count);
    }

    [Fact]
    public async Task A_block_with_no_example_opens_with_empty_required_fields_and_Save_waits_for_them()
    {
        var page = await Open("start");
        await page.GraphEdit.BeginAdd(Token);

        await page.GraphEdit.AddStep(page.GraphEdit.Groups[1].Blocks[1], Token);

        Assert.Equal([("question", string.Empty)], page.Editor.Fields.Select(f => (f.Name, f.Text)));
        Assert.False(page.CanSave);
        page.Editor.Fields[0].Text = "Ship it?";
        Assert.True(page.CanSave);
    }

    [Fact]
    public async Task The_sheet_focuses_the_first_required_field_not_the_first_field()
    {
        var page = await Open("start");
        await page.GraphEdit.BeginAdd(Token);

        await page.GraphEdit.AddStep(page.GraphEdit.Groups[1].Blocks[2], Token);   // fetch: an optional note comes first, the required url second

        Assert.Equal(["note", "url"], page.Editor.Fields.Select(f => f.Name), StringComparer.Ordinal);
        Assert.Equal("url", page.FocusFieldName);
    }

    [Fact]
    public async Task The_sheet_focuses_the_first_field_when_none_is_required_and_none_when_there_are_no_fields()
    {
        var page = await Open(selected: null);
        await page.GraphEdit.BeginAdd(Token);

        await page.GraphEdit.AddStep(page.GraphEdit.Groups[0].Blocks[0], Token);   // manual: no fields

        Assert.Equal(string.Empty, page.FocusFieldName);
    }

    [Fact]
    public async Task A_step_with_no_settings_can_be_saved_at_once()
    {
        var page = await Open(selected: null);
        await page.GraphEdit.BeginAdd(Token);

        await page.GraphEdit.AddStep(page.GraphEdit.Groups[0].Blocks[0], Token);   // manual: no fields

        Assert.True(page.CanSave);
        await page.Save(Token);
        AssertSent(0, """[{"op":"add_node","id":"manual_1","type":"manual"}]""");
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

        await AddAndSave(page);
        page.CloseEditor();   // the stale answer holds the sheet open; cancel it so the graph's own reload can run

        Assert.StartsWith("This workflow changed elsewhere", page.GraphEdit.ReloadPromptText, StringComparison.Ordinal);
        Assert.Equal((false, false, false), (page.GraphEdit.CanAdd, page.GraphEdit.CanRemove, page.GraphEdit.CanConnect));

        await page.GraphEdit.Reload(Token);

        Assert.Equal((string.Empty, true), (page.GraphEdit.ReloadPromptText, page.GraphEdit.CanAdd));
    }

    [Fact]
    public async Task An_edit_after_Reload_is_sent_on_the_revision_the_reload_read()
    {
        var page = await Open("start");
        await page.GraphEdit.BeginAdd(Token);
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem("workflows-slug-ops", "ops-stale");
        await AddAndSave(page);
        page.CloseEditor();
        var newer = Golden("workflows-slug", "found");
        newer["revision"] = 3;
        newer["latestRevision"] = 3;
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, newer.ToJsonString());
        await page.GraphEdit.Reload(Token);
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Api("workflows-slug-ops", "ops-applied");
        Assert.True(page.Runs.Graph!.SelectNode("start"));

        await AddAndSave(page);

        Assert.Equal(3, _f.Server.AppliedWorkflowOps[^1].BaseRevision);
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
    public async Task An_add_step_on_a_revision_the_picker_read_goes_out_on_that_base_and_Sindris_stale_answer_asks_to_reload()
    {
        var page = await Open("agent");
        await page.GraphEdit.BeginAdd(Token);   // the editor read revision 2 here; the page shows 2, so nothing is held
        var newer = Golden("workflows-slug", "found");
        newer["latestRevision"] = 3;
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, newer.ToJsonString());   // saved elsewhere after the picker opened
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem("workflows-slug-ops", "ops-stale");   // Sindri refuses a stale base

        await AddAndSave(page);

        Assert.Equal(2, _f.Server.AppliedWorkflowOps.Single().BaseRevision);   // never retried on another base
        Assert.True(page.Editor.NeedsReload);
        Assert.True(page.GraphEdit.HasReloadPrompt);
    }

    [Fact]
    public async Task A_connect_on_a_revision_the_page_never_showed_sends_nothing_and_asks_to_reload()
    {
        var page = await Open("agent");
        var newer = Golden("workflows-slug", "found");
        newer["latestRevision"] = 3;
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, newer.ToJsonString());

        page.GraphEdit.BeginConnect();
        await page.GraphEdit.ConnectTo("start", Token);

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
