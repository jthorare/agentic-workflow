using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3682 item 1: the Code step's sheet draws a tall monospace code box that takes Tab and Enter, the block's example under it, and keeps Repository, Branch, Stop after and Memory limit behind a closed Advanced toggle.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsCodeStepFramesTests : FrameHost
{
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Blocks = """
        {"groups":[
          {"group":"Triggers","blocks":[{"type":"manual","title":"Manual","isTrigger":true,"config":[],"outputs":{},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]},
          {"group":"Logic","blocks":[{"type":"code","title":"Code","isTrigger":false,
            "config":[{"name":"input","type":"text","required":false,"caption":"Input"},{"name":"code","type":"text","required":true,"caption":"Code"},
                      {"name":"repo","type":"text","required":false,"caption":"Repository"},{"name":"branch","type":"text","required":false,"caption":"Branch"},
                      {"name":"timeout","type":"text","required":false,"caption":"Stop after"},{"name":"memoryMb","type":"number","required":false,"caption":"Memory limit (MB)"}],
            "outputs":{"result":"any"},"branches":["out"],"exampleConfig":{"input":"{{start.x}}","code":"return input;"},"exampleSpec":null}]}
        ]}
        """;
#pragma warning restore MA0136

    [Fact]
    public Task The_code_step_has_a_tall_code_box_with_its_example_and_the_rarely_used_settings_under_Advanced() => OnUi(async () =>
    {
        var workflow = Golden("workflows-slug", "found");
        var node = workflow["spec"]!["nodes"]![0]!;
        (node["type"], node["config"]) = ("code", new JsonObject { ["code"] = "return 1;" });
        Server.WorkflowAnswers[Slug] = new RouteAnswer(System.Net.HttpStatusCode.OK, workflow.ToJsonString());
        Server.WorkflowRunsAnswers[Slug] = RouteAnswer.Api("workflows-slug-runs", "list-three");
        Server.WorkflowBlocksAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, Blocks);
        var (window, shell) = await Connected(width: 1280, height: 1000);
        shell.GoTo(PageId.Workflows);
        await Until(window, () => shell.Workflows.Rows.Count > 0, "the Workflows page never read the list");
        await shell.Workflows.OpenWorkflow(Slug, CancellationToken.None);
        await Until(window, () => shell.Workflows.Runs.Graph is not null, "the graph never showed");
        Assert.True(shell.Workflows.Runs.Graph!.SelectNode("start"));
        await shell.Workflows.OpenSettings(CancellationToken.None);
        await Until(window, () => shell.Workflows.Editor.Fields.Count > 0 && Visible(window, "EditorFields"), "the editor never showed the code step's fields");
        Settle(window);

        var fields = Named<ItemsControl>(window, "EditorFields");
        var box = fields.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "EditorFieldCode");
        Assert.True(box.IsEffectivelyVisible, "the code box is hidden");
        Assert.True(box.AcceptsReturn && box.AcceptsTab, "the code box does not take Enter and Tab");
        Assert.True(box.Bounds.Height >= 200, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the code box is {box.Bounds.Height}px tall: it needs about 12 rows"));
        Assert.Contains("monospace", box.FontFamily.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fields.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && string.Equals(t.Text, "return input;", StringComparison.Ordinal));   // the example under the box

        var repo = fields.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "EditorFieldText" && ((ViewModels.Workflows.ConfigFieldViewModel)t.DataContext!).Name == "repo");
        Assert.False(repo.IsEffectivelyVisible, "Repository shows before Advanced is opened");
        var toggle = Named<ToggleButton>(window, "EditorAdvancedGroup");
        toggle.IsChecked = true;
        await Until(window, () => repo.IsEffectivelyVisible, "Advanced never showed Repository");
    });
}
