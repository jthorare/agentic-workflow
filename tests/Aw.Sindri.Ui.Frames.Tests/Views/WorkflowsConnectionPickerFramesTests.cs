using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3690: a connection step's sheet draws a picker over the connected connections, or a Connect GitHub button when none fits, and never a free text box for the connection. Pressing the button asks the shell for Connections.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsConnectionPickerFramesTests : FrameHost
{
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Blocks = """
        {"groups":[
          {"group":"Triggers","blocks":[{"type":"connection_event","title":"Connection event","isTrigger":true,
            "config":[{"name":"connection","type":"text","required":true,"caption":"Connection"},{"name":"event","type":"text","required":true,"caption":"Event"}],
            "outputs":{},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]}
        ]}
        """;
#pragma warning restore MA0136

    private async Task<(Window Window, Aw.Sindri.Ui.ViewModels.Shell.ShellViewModel Shell)> OnTheSheet()
    {
        var workflow = Golden("workflows-slug", "found");
        var node = workflow["spec"]!["nodes"]![0]!;
        (node["type"], node["config"]) = ("connection_event", new JsonObject { ["event"] = "github.pr.merged" });
        Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, workflow.ToJsonString());
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.WorkflowBlocksAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, Blocks);
        var (window, shell) = await Connected(width: 1280, height: 1000);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Graph is not null, "the graph never showed");
        Assert.True(shell.Workflows.Runs.Graph!.SelectNode("start"));
        await shell.Workflows.Edit(CancellationToken.None);
        await Until(window, () => shell.Workflows.Editor.Fields.Count > 0 && Visible(window, "EditorFields"), "the sheet never showed its fields");
        Settle(window);
        return (window, shell);
    }

    [Fact]
    public Task With_no_connection_the_sheet_shows_Connect_GitHub_and_no_text_box_and_pressing_it_opens_Connections() => OnUi(async () =>
    {
        var (window, shell) = await OnTheSheet();

        var fields = Named<ItemsControl>(window, "EditorFields");
        var field = shell.Workflows.Editor.Fields.Single(f => f.Name == "connection");
        var connect = fields.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "EditorFieldConnect" && b.DataContext == field);
        Assert.True(connect.IsEffectivelyVisible);
        Assert.Equal("Connect GitHub", connect.Content);
        var text = fields.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "EditorFieldText" && t.DataContext == field);
        Assert.False(text.IsEffectivelyVisible, "the connection is still a free text box");

        shell.Workflows.ConnectService(field);   // what the button's click calls
        await Until(window, () => shell.CurrentPage == PageId.Connections, "Connect GitHub never opened Connections");
    });

    [Fact]
    public Task With_a_connected_GitHub_connection_the_sheet_shows_a_picker_and_no_Connect_button() => OnUi(async () =>
    {
        _ = Server.SeedConnection(FakeSindriApi.ConnectionRow("github:work"));
        var (window, shell) = await OnTheSheet();

        var fields = Named<ItemsControl>(window, "EditorFields");
        var field = shell.Workflows.Editor.Fields.Single(f => f.Name == "connection");
        var picker = fields.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "EditorFieldPresets" && c.DataContext == field);
        Assert.True(picker.IsEffectivelyVisible);
        Assert.Equal(1, picker.ItemCount);
        Assert.DoesNotContain(fields.GetVisualDescendants().OfType<Button>(), b => b.Name == "EditorFieldConnect" && b.DataContext == field && b.IsEffectivelyVisible);
    });
}
