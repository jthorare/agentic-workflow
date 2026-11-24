using System.Net;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>A card draws one settings line, cut with an ellipsis when it is long (#4203): every card on a row is the same height, and the full line is in the
/// card's tooltip.</summary>
[Lane(TestLane.Integration)]
public sealed class GraphCardHeightTests
{
    private const string Line = "Every weekday 7:00 America/Los_Angeles";   // 38 characters: past one card line

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(GraphCardHeightTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_Schedule_card_with_a_long_settings_line_is_as_tall_as_the_Notify_card_beside_it()
    {
        var graph = await Graph();

        _ = graph.Relayout(1440, 728, 1.0);

        var (when, tell) = (graph.Nodes.Single(n => string.Equals(n.Id, "when", StringComparison.Ordinal)), graph.Nodes.Single(n => string.Equals(n.Id, "tell", StringComparison.Ordinal)));
        Assert.Equal(when.Y, tell.Y, 3);   // one row
        Assert.Equal(tell.Height, when.Height, 3);
    }

    [Fact]
    public async Task The_cards_tooltip_holds_its_full_settings_line()
    {
        var graph = await Graph();

        var tip = await OnUi(() =>
        {
            var canvas = new GraphCanvas { Graph = graph };
            var window = new Window { Content = canvas, Width = 1440, Height = 728 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            try
            {
                return canvas.TipAt(canvas.NodeRect("when")!.Value.Center);
            }
            finally
            {
                window.Close();
            }
        });

        Assert.Equal("Weekday run · " + Line, tip, StringComparer.Ordinal);
    }

    // A Schedule card with the long line and a Notify card after it, as GET /sindri/workflows/{slug} serves them.
    private async Task<WorkflowGraphViewModel> Graph()
    {
        var json = Golden("workflows-slug", "found");
        json["spec"]!["nodes"] = JsonNode.Parse("""[{"id":"when","type":"schedule","title":"Weekday run","config":{}},{"id":"tell","type":"notify","title":"Tell the owner","config":{"title":"Done"}}]""");
        json["spec"]!["edges"] = JsonNode.Parse("""[{"from":"when","to":"tell"}]""");
        json["positions"] = JsonNode.Parse("""[{"node":"when","x":0,"y":0},{"node":"tell","x":280,"y":0}]""");
        json["summaries"] = JsonNode.Parse("[{\"node\":\"when\",\"summary\":\"" + Line + "\",\"summaryProblem\":null,\"kindTitle\":\"Schedule\"},{\"node\":\"tell\",\"summary\":\"Send notification\",\"summaryProblem\":null,\"kindTitle\":\"Send notification\"}]");
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, json.ToJsonString());
        var served = await _f.Server.Workflow(Url, Slug, Token);
        return new WorkflowGraphViewModel(served.Value, statuses: null);
    }
}
