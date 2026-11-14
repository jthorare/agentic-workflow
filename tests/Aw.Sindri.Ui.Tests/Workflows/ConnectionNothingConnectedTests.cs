using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3705: back from Connections with no usable connection, the connection field says "Nothing connected yet." and the Connect button stays. With a connection it says nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionNothingConnectedTests
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

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> PressedConnect()
    {
        var workflow = Golden("workflows-slug", "found");
        var node = workflow["spec"]!["nodes"]![0]!;
        (node["type"], node["config"]) = ("connection_event", new JsonObject { ["event"] = "github.pr.merged" });
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, workflow.ToJsonString());
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        _ = page.Runs.Graph!.SelectNode("start");
        await page.OpenSettings(Token);
        page.ConnectService(page.Editor.Fields.Single(f => f.Name == "connection"));
        page.Back();
        return page;
    }

    [Fact]
    public async Task Back_with_no_connection_the_field_says_nothing_connected_yet_and_still_offers_Connect()
    {
        var page = await PressedConnect();

        await page.ReturnFromConnect(Token);

        var field = page.Editor.Fields.Single(f => f.Name == "connection");
        Assert.Equal(("Nothing connected yet.", true, "Connect GitHub"), (field.NoticeText, field.NeedsConnect, field.ConnectText));
    }

    [Fact]
    public async Task Back_with_a_new_connection_the_field_says_nothing()
    {
        var page = await PressedConnect();
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("github:work"));

        await page.ReturnFromConnect(Token);

        var field = page.Editor.Fields.Single(f => f.Name == "connection");
        Assert.Equal((string.Empty, "github:work"), (field.NoticeText, field.Text));
    }

    [Fact]
    public async Task Before_Connect_was_pressed_the_field_says_nothing()
    {
        var page = await PressedConnect();   // pressed once, then back on the list: the sheet is shut
        page.CloseEditor();
        await page.OpenWorkflow(Slug, Token);
        _ = page.Runs.Graph!.SelectNode("start");

        await page.OpenSettings(Token);   // the sheet reopened by hand, not through ReturnFromConnect

        var field = page.Editor.Fields.Single(f => f.Name == "connection");
        Assert.Equal((string.Empty, true), (field.NoticeText, field.NeedsConnect));
    }
}
