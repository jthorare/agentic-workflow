using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3690: a connection step's connection field is a picker over the connected services that fit, with no free text box; with none that fits it offers "Connect GitHub", which opens Connections and comes back to the same sheet with the new connection picked.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionPickerTests
{
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Blocks = """
        {"groups":[
          {"group":"Triggers","blocks":[{"type":"connection_event","title":"Connection event","isTrigger":true,
            "config":[{"name":"connection","type":"text","required":true,"caption":"Connection"},{"name":"event","type":"text","required":true,"caption":"Event"}],
            "outputs":{},"branches":["out"],"exampleConfig":{"connection":"github:work","event":"github.pr.merged"},"exampleSpec":null}]}
        ]}
        """;
#pragma warning restore MA0136

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> OnTheTrigger()
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
        return page;
    }

    [Fact]
    public async Task The_connection_field_lists_only_the_connected_connections_of_the_service_and_has_no_text_box()
    {
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("github:work"));
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("github:old", "needs-sign-in"));
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("linear:me"));
        var page = await OnTheTrigger();

        var field = page.Editor.Fields.Single(f => f.Name == "connection");

        Assert.Equal((true, false, false), (field.IsConnection, field.NeedsConnect, field.IsRawVisible));
        Assert.Equal(["github:work"], field.ConnectionChoices, StringComparer.Ordinal);
        Assert.False(page.Editor.Fields.Single(f => f.Name == "event").IsConnection);
    }

    [Fact]
    public async Task With_no_connection_that_fits_the_field_offers_Connect_GitHub()
    {
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("linear:me"));
        var page = await OnTheTrigger();

        var field = page.Editor.Fields.Single(f => f.Name == "connection");

        Assert.Equal((true, "Connect GitHub", false), (field.NeedsConnect, field.ConnectText, field.IsRawVisible));
        Assert.Empty(field.ConnectionChoices);
    }

    [Fact]
    public async Task Connect_GitHub_asks_for_Connections_and_the_return_reopens_the_same_sheet_with_the_new_connection_picked()
    {
        var page = await OnTheTrigger();
        string? asked = null;
        page.ConnectRequested += service => asked = service;

        page.ConnectService(page.Editor.Fields.Single(f => f.Name == "connection"));
        Assert.Equal("github", asked);

        page.Back();   // the owner is on Connections now: the page left its runs
        Assert.False(page.IsSettingsOpen);
        _ = _f.Server.SeedConnection(FakeSindriApi.ConnectionRow("github:work"));
        await page.ReturnFromConnect(Token);

        Assert.Equal((true, true, "start"), (page.IsRuns, page.IsSettingsOpen, page.Runs.Graph!.SelectedId));
        var field = page.Editor.Fields.Single(f => f.Name == "connection");
        Assert.Equal(("github:work", true), (field.Text, field.IsChanged));
    }

    [Fact]
    public async Task A_return_with_no_Connect_pressed_does_nothing()
    {
        var page = await _f.Shown(Token);

        await page.ReturnFromConnect(Token);

        Assert.False(page.IsSettingsOpen);
    }
}
