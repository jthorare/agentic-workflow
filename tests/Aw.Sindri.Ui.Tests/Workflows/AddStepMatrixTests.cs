using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>W5, the Add step matrix (spec 4.2, 13 row 3): ten kinds, each three ways. The sheet opens before anything is sent, Cancel adds nothing, an empty required field is refused in the sheet, and Save sends ONE add_node with the filled config.</summary>
[Lane(TestLane.Unit)]
public sealed class AddStepMatrixTests
{
    // kind -> the one required field its form asks for (the catalog serves the real ones; the matrix needs a required text field on each)
    private static readonly (string Type, string Title, string Field)[] _kinds =
    [
        ("http", "HTTP request", "url"),
        ("prompt", "Prompt", "prompt"),
        ("run_agent", "Run agent", "prompt"),
        ("condition", "If", "expression"),
        ("wait", "Wait", "duration"),
        ("code", "Code", "source"),
        ("notify", "Send notification", "message"),
        ("create_item", "Create item", "title"),
        ("memory_write", "Remember", "text"),
        ("call_tool", "Toolkit tool", "tool"),
    ];

    private readonly WorkflowsFixture _f = new();

    public static TheoryData<string> Kinds { get; } = [.. _kinds.Select(k => k.Type)];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Blocks() =>
        new JsonObject
        {
            ["groups"] = new JsonArray(new JsonObject
            {
                ["group"] = "Steps",
                ["blocks"] = new JsonArray([.. _kinds.Select(k => (JsonNode)new JsonObject
                {
                    ["type"] = k.Type,
                    ["title"] = k.Title,
                    ["isTrigger"] = false,
                    ["config"] = new JsonArray(new JsonObject { ["name"] = k.Field, ["type"] = "text", ["required"] = true, ["caption"] = k.Field }),
                    ["outputs"] = new JsonObject(),
                    ["branches"] = new JsonArray("out"),
                    ["exampleConfig"] = null,
                    ["exampleSpec"] = null,
                })]),
            }),
        }.ToJsonString();

    private async Task<WorkflowsPageViewModel> OpenSheet(string kind, string? selected)
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks());
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        if (selected is not null)
        {
            Assert.True(page.Runs.Graph!.SelectNode(selected));
        }

        await page.GraphEdit.BeginAdd(Token);
        await page.GraphEdit.PickBlock(page.GraphEdit.Groups.SelectMany(g => g.Blocks).Single(b => string.Equals(b.Type, kind, StringComparison.Ordinal)), Token);
        return page;
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task Add_step_matrix__cancel_adds_nothing(string kind)
    {
        var page = await OpenSheet(kind, "start");
        var title = _kinds.Single(k => string.Equals(k.Type, kind, StringComparison.Ordinal)).Title;

        Assert.True(page.IsEditing);
        Assert.True(page.Editor.IsNewStep);
        Assert.False(page.GraphEdit.IsAdding);
        Assert.Contains(title, page.EditorHeading, StringComparison.Ordinal);
        Assert.Empty(_f.Server.AppliedWorkflowOps);

        page.CloseEditor();

        Assert.False(page.IsEditing);
        Assert.False(page.Editor.IsNewStep);
        Assert.Empty(_f.Server.AppliedWorkflowOps);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task Add_step_matrix__invalid_is_refused_in_the_sheet_and_sends_nothing(string kind)
    {
        var page = await OpenSheet(kind, "start");

        await page.Save(Token);

        Assert.Empty(_f.Server.AppliedWorkflowOps);
        Assert.True(page.IsEditing);
        Assert.True(page.Editor.IsNewStep);
        Assert.Equal(Labels.WfFieldNeeded, Assert.Single(page.Editor.Fields).ErrorText);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task Add_step_matrix__valid_sends_one_add_node(string kind)
    {
        var page = await OpenSheet(kind, "start");
        var field = _kinds.Single(k => string.Equals(k.Type, kind, StringComparison.Ordinal)).Field;
        Assert.Single(page.Editor.Fields).Text = "a value";

        await page.Save(Token);

        var sent = Assert.Single(_f.Server.AppliedWorkflowOps);
        Assert.Equal((Slug, 2), (sent.Slug, sent.BaseRevision));
        var expected = $$$"""[{"op":"add_node","id":"{{{kind}}}_1","type":"{{{kind}}}","config":{"{{{field}}}":"a value"}},{"op":"connect","from":"start","to":"{{{kind}}}_1"}]""";
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(sent.Ops)), sent.Ops);
        Assert.False(page.IsEditing);
        Assert.False(page.Editor.IsNewStep);
    }

    [Fact]
    public async Task With_nothing_selected_Save_sends_only_the_add_node()
    {
        var page = await OpenSheet("prompt", selected: null);
        Assert.Single(page.Editor.Fields).Text = "Say hi";

        await page.Save(Token);

        var sent = Assert.Single(_f.Server.AppliedWorkflowOps);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""[{"op":"add_node","id":"prompt_1","type":"prompt","config":{"prompt":"Say hi"}}]"""), JsonNode.Parse(sent.Ops)), sent.Ops);
    }

    [Fact]
    public async Task A_stale_base_keeps_the_sheet_and_the_text_and_asks_for_a_reload()
    {
        var page = await OpenSheet("prompt", "start");
        Assert.Single(page.Editor.Fields).Text = "Say hi";
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-GRAPH-007", "The workflow changed.");

        await page.Save(Token);

        Assert.True(page.IsEditing);
        Assert.True(page.Editor.NeedsReload);
        Assert.Equal("Say hi", Assert.Single(page.Editor.Fields).Text);
    }

    [Fact]
    public async Task The_steps_layout_still_adds_the_step_at_once_with_no_sheet()
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks());
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        page.GraphEdit.Layout = WorkflowEditorLayout.Steps;
        await page.GraphEdit.BeginAdd(Token);

        await page.GraphEdit.PickBlock(page.GraphEdit.Groups[0].Blocks[0], Token);

        _ = Assert.Single(_f.Server.AppliedWorkflowOps);
        Assert.False(page.IsEditing);
    }

    [Fact]
    public async Task The_page_reads_the_default_layout_as_sheet_until_the_profile_carries_the_key()
    {
        var page = await _f.Shown(Token);

        Assert.Equal(WorkflowEditorLayout.Sheet, page.GraphEdit.Layout);
    }
}
