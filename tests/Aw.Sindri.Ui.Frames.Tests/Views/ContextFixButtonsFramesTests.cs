using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The Context page's empty and error states, each with the one button that fixes it (#3060), at 1280 and 1000: a zoom miss with Clear, a failed node panel
/// with Retry, a failed section with Retry, and a refused link launch with Try again on the node panel and on the inspector's card. Each saves its frame
/// before its checks, under frames/ as f3-context-fix-*.png when AW_UI_FRAMES=1.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ContextFixButtonsFramesTests : FrameHost
{
    private const string Node = "feature:tabs.tab-manager";
    private const string Surface = "surface:command/switch-tab";
    private const string Ticket = "record:linear:issue/0e5a1b00-0000-4000-8000-000000000203";
    private const string LinearUrl = "https://linear.app/split-panes/issue/SPL-103/tab-manager-spec";

    private static string Anchor => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    // The node's answer: its kind as given, a ticket source with a web link, and one edge out.
    private static RouteAnswer NodeBody(string kind) => new(HttpStatusCode.OK, new JsonObject
    {
        ["mapVersion"] = "v1",
        ["node"] = new JsonObject
        {
            ["key"] = Node,
            ["kind"] = kind,
            ["label"] = "Tab manager",
            ["provenance"] = "human",
            ["confirmed"] = true,
            ["trust"] = "trusted",
            ["confidence"] = 1,
            ["source"] = new JsonObject { ["kind"] = "ticket", ["ref"] = "LIN-12", ["url"] = "https://linear.app/x/issue/LIN-12" },
        },
        ["aliases"] = new JsonArray(),
        ["out"] = new JsonArray(new JsonObject { ["from"] = Node, ["type"] = string.Equals(kind, "action", StringComparison.Ordinal) ? "on" : "touches", ["to"] = Surface, ["provenance"] = "human", ["confirmed"] = true }),
        ["in"] = new JsonArray(),
    }.ToJsonString());

    // The golden lens with the ticket's record cited, with its permalink.
    private static RouteAnswer CitedLens()
    {
        var body = GoldenFiles.Node("context-lenses", "map-tabs");
        var record = body["bands"]!["intent"]!["records"]!.AsArray().First(r => (string)r!["key"]! == Ticket)!;
        record["cites"] = new JsonArray("C1");
        record["state"] = "current";
        body["cites"] = new JsonObject
        {
            ["C1"] = new JsonObject
            {
                ["key"] = "linear:issue/0e5a1b00-0000-4000-8000-000000000203",
                ["refs"] = new JsonArray(),
                ["permalink"] = LinearUrl,
                ["trust"] = "untrusted",
                ["state"] = "current",
                ["aliases"] = new JsonArray("SPL-103"),
            },
        };
        return new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
    }

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Open(double width)
    {
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowWhole, "the whole map never drew");
        Settle(window);
        return (window, shell);
    }

    private static async Task Click(ShellViewModel shell, string key)
    {
        var rect = shell.Context.Whole.Geometry!.Nodes.First(n => n.Key == key).Rect;
        shell.Context.Whole.PointerClicked(rect.CenterX, rect.CenterY);
        await shell.Context.Whole.Panel.Reading;
    }

    private static string Name(Window window, string button) => AutomationProperties.GetName(Named<Button>(window, button)) ?? string.Empty;

    private static string Stem(string state, double width) => string.Create(CultureInfo.InvariantCulture, $"context-fix-{state}-{width:0}");

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task A_zoom_that_finds_nothing_on_the_whole_map_is_the_miss_sentence_with_Clear(double width) => OnUi(async () =>
    {
        Server.LensesAnswer = _ => new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Node("context-lenses", "map-tabs").ToJsonString());
        var (window, shell) = await Open(width);
        shell.Context.AnchorText = "zzqq nothing like it";
        await shell.Context.Submit(Token);
        await Until(window, () => Visible(window, "ContextMissClear"), "the miss sentence never drew");

        Frame(window, Stem("zoom-miss", width), "f3-");

        Assert.Equal(Labels.ContextWholeNoMatch, shell.Context.Problem);
        Assert.False(Visible(window, "ContextProblem"));   // the miss is not the refusal banner
        Assert.Equal(Labels.ContextSearchClear, Name(window, "ContextMissClear"));
    });

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task A_failed_node_panel_draws_its_Retry_button(double width) => OnUi(async () =>
    {
        Server.NodeAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-CONTEXT-041", "the map can't be read");
        var (window, shell) = await Open(width);
        await Click(shell, Node);
        await Until(window, () => Visible(window, "WholeNodeRetry"), "the Retry button never drew");

        Frame(window, Stem("node-retry", width), "f3-");

        Assert.True(Visible(window, "WholeNodeFailed"));
        Assert.Equal((Labels.ContextWholeRetry, Labels.ContextNodePanelRetryName), ((string?)Named<Button>(window, "WholeNodeRetry").Content, Name(window, "WholeNodeRetry")));
    });

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task A_failed_section_draws_its_own_Retry_button(double width) => OnUi(async () =>
    {
        Server.NodeAnswer = NodeBody("action");
        Server.LensesAnswer = _ => RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-CONTEXT-041", "the map can't be read");
        var (window, shell) = await Open(width);
        await Click(shell, Node);
        await Until(window, () => Visible(window, "WholeSectionRetry"), "the section's Retry button never drew");

        Frame(window, Stem("section-retry", width), "f3-");

        Assert.False(Visible(window, "WholeNodeRetry"));   // the node read; only its Why section failed
        Assert.Equal(Labels.ContextNodePanelSectionRetryName(Labels.ContextWholeBandWhy), Name(window, "WholeSectionRetry"));
    });

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task A_refused_launch_from_the_node_panel_draws_Try_again(double width) => OnUi(async () =>
    {
        Server.NodeAnswer = NodeBody("feature");
        var (window, shell) = await Open(width);
        await Click(shell, Node);
        await shell.Context.Whole.Panel.OpenSource(Token);   // the frames' launcher refuses every request
        await Until(window, () => Visible(window, "WholeNodeLaunchRetry"), "Try again never drew");

        Frame(window, Stem("launch-retry", width), "f3-");

        Assert.True(Visible(window, "WholeNodeLaunchFailed"));
        Assert.Equal((Labels.ContextPagesRetry, Labels.ContextLaunchRetryName), ((string?)Named<Button>(window, "WholeNodeLaunchRetry").Content, Name(window, "WholeNodeLaunchRetry")));
    });

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task A_refused_launch_from_the_inspector_card_draws_Try_again(double width) => OnUi(async () =>
    {
        Server.LensesAnswer = _ => CitedLens();
        var (window, shell) = await Open(width);
        shell.Context.AnchorText = Anchor;
        await shell.Context.Submit(Token);
        await shell.Context.Select(Ticket, Token);
        await shell.Context.Node.OpenRecord(Token);   // the frames' launcher refuses every request
        await Until(window, () => Visible(window, "NodeLaunchRetry"), "the card's Try again never drew");

        Frame(window, Stem("card-launch-retry", width), "f3-");

        Assert.True(Visible(window, "NodeLaunchFailed"));
        Assert.Equal((Labels.ContextPagesRetry, Labels.ContextLaunchRetryName), ((string?)Named<Button>(window, "NodeLaunchRetry").Content, Name(window, "NodeLaunchRetry")));
    });
}
