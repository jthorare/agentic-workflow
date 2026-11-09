using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
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

        await page.Edit(Token);

        Assert.Equal((true, true), (page.IsEditing, page.CanEdit));
        Assert.Equal(["prompt", "strict"], page.Editor.Fields.Select(f => f.Name), StringComparer.Ordinal);
        Assert.Equal("Look for drift", page.Editor.Fields[0].Text);
        Assert.Equal("Edit Morning check \u00b7 revision 2", page.EditorHeading);
        page.CloseEditor();
        Assert.False(page.IsEditing);
    }

    [Fact]
    public async Task Save_sends_set_config_for_the_changed_field_at_the_read_revision()
    {
        var page = await OnRuns();
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.Edit(Token);
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
        await page.Edit(Token);
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
    public async Task Edit_is_off_for_an_archived_workflow_and_when_no_workflow_is_open()
    {
        var golden = Golden(Slug, "found");
        golden["archivedAt"] = "2026-01-01T00:00:00Z";
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
        var page = await OnRuns();
        Assert.False(page.CanEdit);

        await page.Edit(Token);

        Assert.False(page.IsEditing);
        var closed = _f.Page();
        Assert.False(closed.CanEdit);
    }
}
