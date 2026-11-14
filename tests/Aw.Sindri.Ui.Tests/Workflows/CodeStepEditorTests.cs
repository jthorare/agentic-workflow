using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3682 item 1: the Code step's form has a multi-line code box with the block's example under it, the rarely used settings sit in a closed Advanced group, and the heading names the step once.</summary>
[Lane(TestLane.Unit)]
public sealed class CodeStepEditorTests
{
    private const string Example = "return input;";

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

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<OpsEditor> Loaded(string? title = null)
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        var workflow = Golden("workflows-slug", "found");
        var node = workflow["spec"]!["nodes"]![1]!;
        node["type"] = "code";
        node["config"] = new System.Text.Json.Nodes.JsonObject { ["code"] = "return 1;" };
        _ = node.AsObject().Remove("title");   // the golden's node has its own title
        if (title is not null)
        {
            node["title"] = title;
        }

        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, workflow.ToJsonString());
        var editor = new OpsEditor(_f.Server);
        await editor.Load(Url, Slug, Token);
        editor.Select(workflow["spec"]!["nodes"]![1]!["id"]!.GetValue<string>());
        return editor;
    }

    [Fact]
    public async Task The_code_field_is_a_multi_line_code_box_with_the_blocks_example_and_the_other_fields_are_not()
    {
        var editor = await Loaded();

        var code = editor.Fields.Single(f => f.Name == "code");
        Assert.Equal((true, true, false, Example), (code.IsCode, code.IsMultiline, code.IsSingleLine, code.Example));
        Assert.Equal("return 1;", code.Text);
        var input = editor.Fields.Single(f => f.Name == "input");
        Assert.Equal((false, false, string.Empty), (input.IsCode, input.IsMultiline, input.Example));
    }

    [Fact]
    public async Task Repository_branch_stop_after_and_memory_limit_sit_in_a_closed_Advanced_group_until_it_is_opened()
    {
        var editor = await Loaded();

        Assert.True(editor.HasAdvancedFields);
        Assert.False(editor.AdvancedOpen);
        Assert.Equal(["input", "code"], editor.Fields.Where(f => f.IsShown).Select(f => f.Name), StringComparer.Ordinal);
        Assert.Equal(["repo", "branch", "timeout", "memoryMb"], editor.Fields.Where(f => f.InAdvanced).Select(f => f.Name), StringComparer.Ordinal);

        editor.AdvancedOpen = true;

        Assert.Equal(6, editor.Fields.Count(f => f.IsShown));
    }

    [Fact]
    public async Task A_block_with_no_advanced_settings_has_no_Advanced_group()
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        var editor = new OpsEditor(_f.Server);
        await editor.Load(Url, Slug, Token);
        editor.Select("start");

        Assert.False(editor.HasAdvancedFields);
    }

    [Fact]
    public async Task The_heading_says_the_steps_title_once_when_it_is_the_blocks_own_title()
    {
        Assert.Equal("Code", (await Loaded()).NodeHeading);
        Assert.Equal("Check the feed · Code", (await Loaded("Check the feed")).NodeHeading);
    }
}
