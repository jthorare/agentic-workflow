using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The node panel's failed read has one button that fixes it (#3060): Retry reads the same node again.</summary>
[Lane(TestLane.Unit)]
public sealed class NodePanelRetryTests
{
    private const string Node = "feature:tabs.tab-manager";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NodeView View(string key) =>
        new("v1", new NodeInfo(key, "feature", "Tab manager", "test", Confirmed: true, "high", Confidence: 1.0), Aliases: [], Out: [], In: []);

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
