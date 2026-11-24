using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The node panel's failed states each have one button that fixes it (#3060): Retry reads the same node again, a failed section's Retry reads that section only.</summary>
[Lane(TestLane.Unit)]
public sealed class NodePanelRetryTests
{
    private const string Node = "feature:tabs.tab-manager";
    private const string Submit = "action:schedule/submit";
    private const string Page = "surface:screen/schedule-week";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NodeView View(string key) =>
        new("v1", new NodeInfo(key, "feature", "Tab manager", "test", Confirmed: true, "high", Confidence: 1.0), Aliases: [], Out: [], In: []);

    private static NodeView Action() =>
        new("v1", new NodeInfo(Submit, "action", "Submit", "test", Confirmed: true, "high", Confidence: 1.0), Aliases: [], Out: [new NodeEdge(Submit, "on", Page, "test", Confirmed: true)], In: []);

    private static LensesView Lens()
    {
        var read = AwJson.Deserialize<LensesView>(Encoding.UTF8.GetBytes(GoldenFiles.Node("context-lenses", "map-tabs").ToJsonString()), SindriApiClient.Json);
        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : string.Empty);
        return read.Value;
    }

    private static NodeSectionViewModel Why(NodePanelViewModel panel) => panel.Sections.Single(s => string.Equals(s.Heading, Labels.ContextWholeBandWhy, StringComparison.Ordinal));

    [Fact]
    public async Task Retry_on_a_failed_section_reads_that_section_again_and_not_the_node()
    {
        var (nodeReads, whyReads, whyFails) = (0, 0, true);
        var panel = new NodePanelViewModel(_ => null, _ => { })
        {
            Loader = (_, _) =>
            {
                nodeReads++;
                return Task.FromResult<Result<NodeView>?>(Results.Success(Action()));
            },
            WhyLoader = (_, _) =>
            {
                whyReads++;
                return Task.FromResult<Result<LensesView>?>(whyFails ? Results.Failure<LensesView>(new Error("SND-CONTEXT-041", ErrorKind.Unavailable, "the map can't be read")) : Results.Success(Lens()));
            },
        };
        await panel.Open(Submit, Token);
        Assert.True(Why(panel).IsFailed);
        whyFails = false;

        await Why(panel).Retry(Token);

        // why: a failed section had only its text; the one way out was to read the whole node again (#3060)
        Assert.Equal((false, true), (Why(panel).IsFailed, Why(panel).HasRows));
        Assert.Equal((1, 2), (nodeReads, whyReads));
    }

    [Fact]
    public async Task A_failed_section_offers_Retry_by_its_heading()
    {
        var panel = new NodePanelViewModel(_ => null, _ => { })
        {
            Loader = (_, _) => Task.FromResult<Result<NodeView>?>(Results.Success(Action())),
            WhyLoader = (_, _) => Task.FromResult<Result<LensesView>?>(Results.Failure<LensesView>(new Error("SND-CONTEXT-041", ErrorKind.Unavailable, "the map can't be read"))),
        };

        await panel.Open(Submit, Token);

        Assert.Equal((true, Labels.ContextWholeRetry, "Read Why again"), (Why(panel).CanRetry, Why(panel).RetryText, Why(panel).RetryName));
        Assert.All(panel.Sections.Where(s => !s.IsFailed), s => Assert.False(s.CanRetry));
    }

    [Fact]
    public async Task Retry_on_a_failed_panel_reads_the_same_node_again_and_shows_it()
    {
        var asked = new List<string>();
        var fail = true;
        var panel = new NodePanelViewModel(_ => null, _ => { })
        {
            Loader = (key, _) =>
            {
                asked.Add(key);
                return Task.FromResult<Result<NodeView>?>(fail ? Results.Failure<NodeView>(new Error("SND-CONTEXT-041", ErrorKind.Unavailable, "the map can't be read")) : Results.Success(View(key)));
            },
        };
        await panel.Open(Node, Token);
        Assert.True(panel.IsFailed);
        fail = false;

        await panel.Retry(Token);

        // why: the failed panel had only its text, so the one way out was to close it and click the node again (#3060)
        Assert.Equal((false, false, "Tab manager"), (panel.IsFailed, panel.IsLoading, panel.Title));
        Assert.Equal([Node, Node], asked, StringComparer.Ordinal);
        Assert.Equal((Labels.ContextWholeRetry, Labels.ContextNodePanelRetryName), (panel.RetryText, panel.RetryName));
    }
}
