using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The workflow editor sheet's page logic (#2258 PR 17 part 3a): Edit opens it on the selected node, Save sends the changed fields on the revision read, a stale answer asks for a reload, and an archived workflow can't be edited.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsEditorSheetTests
{
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Blocks = """
        {"groups":[
          {"group":"Triggers","blocks":[{"type":"manual","title":"Manual","isTrigger":true,"config":[],"outputs":{},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]},
          {"group":"AI","blocks":[{"type":"run_agent","title":"Run an agent","isTrigger":false,
            "config":[{"name":"prompt","type":"text","required":true},{"name":"strict","type":"boolean","required":false}],
            "outputs":{"text":"text"},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]}
        ]}
        """;
#pragma warning restore MA0136

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> OnRuns()
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        return page;
    }

    [Fact]
    public async Task Edit_opens_the_sheet_on_the_selected_nodes_fields()
    {
        var page = await OnRuns();
        Assert.True(page.Runs.Graph!.SelectNode("agent"));

        await page.OpenSettings(Token);

        Assert.Equal((true, true), (page.IsSettingsOpen, page.CanEdit));
        Assert.Equal(["prompt", "strict"], page.Editor.Fields.Select(f => f.Name), StringComparer.Ordinal);
        Assert.Equal("EditorFieldCheck.strict", page.Editor.Fields[1].CheckAutomationId);   // #3111: a driver targets one field's checkbox by this id
        Assert.Equal("Look for drift", page.Editor.Fields[0].Text);
        Assert.Equal("Check the repos \u00b7 Run an agent", page.EditorHeading);   // the step's title and its kind, never the node id or the revision
        page.CloseEditor();
        Assert.False(page.IsSettingsOpen);
    }

    [Fact]
    public async Task Save_sends_set_config_for_the_changed_field_at_the_read_revision()
    {
        var page = await OnRuns();
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.OpenSettings(Token);
        page.Editor.Fields[0].Text = "Check twice";

        await page.Save(Token);

        var sent = Assert.Single(_f.Server.AppliedWorkflowOps);
        Assert.Equal((Slug, 2), (sent.Slug, sent.BaseRevision));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""[{"op":"set_config","node":"agent","path":"prompt","value":"Check twice"}]"""), JsonNode.Parse(sent.Ops)), sent.Ops);
        Assert.Equal(("Save", false), (page.SaveText, page.Editor.IsSending));
    }

    [Fact]
    public async Task A_stale_answer_shows_the_reload_prompt_and_Reload_reads_the_new_revision()
    {
        var page = await OnRuns();
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.OpenSettings(Token);
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem("workflows-slug-ops", "ops-stale");
        page.Editor.Fields[0].Text = "Check twice";

        await page.Save(Token);

        Assert.Equal((true, false), (page.Editor.NeedsReload, page.CanSave));
        Assert.StartsWith("This workflow changed elsewhere", page.Editor.ReloadPromptText, StringComparison.Ordinal);

        var reads = _f.Server.WorkflowCalls;
        await page.Editor.Reload(Token);

        Assert.Equal(reads + 1, _f.Server.WorkflowCalls);
        Assert.Equal((false, true), (page.Editor.NeedsReload, page.CanSave));
    }

    [Fact]
    public async Task A_saved_edit_whose_reload_fails_says_it_saved_and_the_reload_failed()
    {
        var page = await OnRuns();
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.OpenSettings(Token);
        page.Editor.Fields[0].Text = "Check twice";
        _f.Server.WorkflowAnswers[Slug] = RouteAnswer.Problem("workflows-slug", "not-found");

        await page.Save(Token);

        _ = Assert.Single(_f.Server.AppliedWorkflowOps);
        Assert.StartsWith("The edit was saved, but", page.Editor.FailureText, StringComparison.Ordinal);
        Assert.DoesNotContain("Couldn't read the workflow to edit", page.Editor.FailureText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_sends_two_changed_fields_as_one_ops_call()
    {
        var page = await OnRuns();
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.OpenSettings(Token);
        page.Editor.Fields[0].Text = "Check twice";
        page.Editor.Fields[1].Text = "true";

        await page.Save(Token);

        var sent = Assert.Single(_f.Server.AppliedWorkflowOps);
        Assert.Equal(2, sent.BaseRevision);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse("""[{"op":"set_config","node":"agent","path":"prompt","value":"Check twice"},{"op":"set_config","node":"agent","path":"strict","value":true}]"""),
            JsonNode.Parse(sent.Ops)), sent.Ops);
    }

    [Fact]
    public async Task A_refused_save_keeps_every_edit_in_the_form_and_the_sheet_open()
    {
        var page = await OnRuns();
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.OpenSettings(Token);
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem("workflows-slug-ops", "ops-stale");
        page.Editor.Fields[0].Text = "Check twice";
        page.Editor.Fields[1].Text = "true";

        await page.Save(Token);

        _ = Assert.Single(_f.Server.AppliedWorkflowOps);
        Assert.Equal((true, true, true), (page.IsSettingsOpen, page.Editor.NeedsReload, page.Editor.Fields.All(f => f.IsChanged)));
        Assert.Equal(("Check twice", "true"), (page.Editor.Fields[0].Text, page.Editor.Fields[1].Text));
    }

    [Fact]
    public async Task Edit_is_off_for_an_archived_workflow_and_when_no_workflow_is_open()
    {
        var golden = Golden("workflows-slug", "found");
        golden["archivedAt"] = "2026-01-01T00:00:00Z";
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
        var page = await OnRuns();
        Assert.False(page.CanEdit);

        await page.OpenSettings(Token);

        Assert.False(page.IsSettingsOpen);
        var closed = _f.Page();
        Assert.False(closed.CanEdit);
    }

    [Fact]
    public async Task Leaving_the_workflow_closes_the_sheet_and_Save_sends_nothing_for_it()
    {
        const string other = "evening-check";
        _f.Server.WorkflowAnswers[other] = RouteAnswer.Api("workflows-slug", "found");
        var page = await OnRuns();
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.OpenSettings(Token);
        page.Editor.Fields[0].Text = "Check twice";

        await page.OpenWorkflow(other, Token);   // straight from the runs level: OpenWorkflow itself closes the sheet
        await page.Save(Token);

        Assert.False(page.IsSettingsOpen);
        Assert.Empty(_f.Server.AppliedWorkflowOps);
    }

    [Fact]
    public async Task Opening_a_run_closes_the_sheet()
    {
        var page = await OnRuns();
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.OpenSettings(Token);

        await page.OpenRun(RunId, Token);

        Assert.False(page.IsSettingsOpen);
    }

    [Fact]
    public async Task CanEdit_is_raised_when_the_shown_workflow_is_archived_on_a_reread()
    {
        var page = await OnRuns();
        Assert.True(page.CanEdit);
        var raised = new List<string>();
        page.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);
        var golden = Golden("workflows-slug", "found");
        golden["archivedAt"] = "2026-01-01T00:00:00Z";
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());

        await page.Runs.Read(Url, Slug, "Morning check", Token);

        Assert.False(page.CanEdit);
        Assert.Contains(nameof(WorkflowsPageViewModel.CanEdit), raised, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_stale_Edit_does_not_select_its_node_on_the_newer_sheet()
    {
        const string Other = "weekly-report";
        _f.Server.WorkflowAnswers[Other] = RouteAnswer.Api("workflows-slug", "found");
        _f.Server.WorkflowRunsAnswers[Other] = RouteAnswer.Api("workflows-slug-runs", "list-empty");
        var page = await OnRuns();
        _ = page.Runs.Graph!.SelectNode("agent");
        var slow = new TaskCompletionSource();
        _f.Server.WorkflowGates[Slug] = slow;
        var staleEdit = page.OpenSettings(Token);

        page.Back();
        await page.OpenWorkflow(Other, Token);
        _ = page.Runs.Graph!.SelectNode("start");
        await page.OpenSettings(Token);
        slow.SetResult();
        await staleEdit;

        Assert.Equal((true, Other), (page.IsSettingsOpen, page.Editor.Slug));
        Assert.Equal("Manual \u00b7 trigger", page.EditorHeading);   // the node has no title: its block's
        Assert.Empty(page.Editor.Fields);
    }

    private JsonNode ServeRevision(int revision)
    {
        var newer = Golden("workflows-slug", "found");
        newer["latestRevision"] = revision;
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, newer.ToJsonString());
        return newer;
    }

    [Fact]
    public async Task A_graph_edit_after_a_sheet_save_goes_out_once_on_the_saved_revision_with_no_reload_prompt()
    {
        var page = await OnRuns();
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.OpenSettings(Token);
        page.Editor.Fields[0].Text = "Check twice";
        _ = ServeRevision(3);   // what the save makes Sindri hold
        await page.Save(Token);
        Assert.Equal(3, page.Runs.Shown!.LatestRevision);   // the page shows the revision the save made
        page.CloseEditor();   // graph edits wait for the sheet to close

        await page.GraphEdit.BeginAdd(Token);
        await page.GraphEdit.AddStep(page.GraphEdit.Groups[1].Blocks[0], Token);
        page.Editor.Fields[0].Text = "Check";
        await page.Save(Token);

        Assert.Equal(2, _f.Server.AppliedWorkflowOps.Count);
        Assert.Equal(3, _f.Server.AppliedWorkflowOps[1].BaseRevision);
        Assert.False(page.Editor.NeedsReload);
        Assert.Equal(3, page.Runs.Shown!.LatestRevision);
    }

    [Fact]
    public async Task The_sheets_Reload_rereads_the_page_too()
    {
        var page = await OnRuns();
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.OpenSettings(Token);
        _ = ServeRevision(3);

        await page.ReloadEditor(Token);

        Assert.Equal(3, page.Editor.BaseRevision);
        Assert.Equal(3, page.Runs.Shown!.LatestRevision);
    }

    [Fact]
    public async Task A_failed_workflow_read_leaves_nothing_of_the_old_workflow_to_save()
    {
        const string other = "evening-check";
        _f.Server.WorkflowAnswers[other] = RouteAnswer.Api("workflows-slug", "found");
        var page = await OnRuns();
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.OpenSettings(Token);
        page.Editor.Fields[0].Text = "Check twice";
        page.CloseEditor();
        await page.OpenWorkflow(other, Token);   // the page reads it
        _ = page.Runs.Graph!.SelectNode("agent");
        _f.Server.WorkflowAnswers[other] = RouteAnswer.Problem("workflows-slug", "not-found");   // the editor's own read fails

        await page.OpenSettings(Token);
        if (page.Editor.Fields.Count > 0)
        {
            page.Editor.Fields[0].Text = "Check three";
        }

        await page.Save(Token);

        Assert.Empty(_f.Server.AppliedWorkflowOps);
        Assert.False(page.CanSave);
    }

    [Fact]
    public async Task With_the_sheet_open_graph_edits_are_off_with_a_line_saying_why_and_typed_text_stays()
    {
        var page = await OnRuns();
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.GraphEdit.BeginAdd(Token);
        await page.OpenSettings(Token);
        page.Editor.Fields[0].Text = "Typed, not saved";

        await page.GraphEdit.AddStep(page.GraphEdit.Groups[1].Blocks[0], Token);

        Assert.Empty(_f.Server.AppliedWorkflowOps);
        Assert.False(page.GraphEdit.CanEdit);
        Assert.Equal(Labels.WfEditSheetOpenNote, page.GraphEdit.SheetOpenText);
        Assert.Equal("Typed, not saved", page.Editor.Fields[0].Text);
        page.CloseEditor();
        Assert.Equal(string.Empty, page.GraphEdit.SheetOpenText);
        Assert.True(page.GraphEdit.CanEdit);
    }

    [Fact]
    public async Task Save_sends_nothing_once_the_shown_workflow_turns_out_archived()
    {
        var page = await OnRuns();
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.OpenSettings(Token);
        page.Editor.Fields[0].Text = "Check twice";
        var archived = ServeRevision(2);
        archived["archivedAt"] = "2026-01-01T00:00:00Z";
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, archived.ToJsonString());
        await page.Runs.Read(Url, Slug, "Morning check", Token);

        await page.Save(Token);

        Assert.Empty(_f.Server.AppliedWorkflowOps);
        Assert.False(page.CanSave);
    }
}
