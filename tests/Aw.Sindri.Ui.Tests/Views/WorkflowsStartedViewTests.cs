using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Workflows;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>A board card in a real window says when its run began and who began it.</summary>
[Lane(TestLane.Integration)]
public sealed class WorkflowsStartedViewTests : FrameHost
{
    [Fact]
    public Task A_card_says_when_and_by_whom_the_run_started() => OnUi(async () =>
    {
        Server.InstancesRewrite = body =>
        {
            var list = System.Text.Json.Nodes.JsonNode.Parse(LineGolden.AsLine(body))!;
            foreach (var row in list["instances"]!.AsArray().Select(r => r!.AsObject()))
            {
                row["startedBy"] = "human:cli";
                row["startedAt"] = "2026-10-11T11:02:00.0000000Z";
            }

            return list.ToJsonString();
        };
        var (window, shell) = await Connected(s => s.Workflows.Lanes.Sum(l => l.Cards.Count) > 0, 1440, 900);
        shell.GoTo(PageId.Workflows);
        Settle(window);

        var cards = window.GetVisualDescendants().OfType<RunCard>().Where(c => c.IsEffectivelyVisible).ToList();

        Assert.NotEmpty(cards);
        Assert.All(cards, c => Assert.Equal("Started 11:02 by you", Named<TextBlock>(c, "WorkflowCardStarted").Text));
    });

    [Fact]
    public Task A_card_names_who_the_run_waits_on() => OnUi(async () =>
    {
        Server.InstancesRewrite = body =>
        {
            var list = System.Text.Json.Nodes.JsonNode.Parse(LineGolden.AsLine(body))!;
            foreach (var row in list["instances"]!.AsArray().Select(r => r!.AsObject()))
            {
                row["waitingOn"] = "agent";
            }

            return list.ToJsonString();
        };
        var (window, shell) = await Connected(s => s.Workflows.Lanes.Sum(l => l.Cards.Count) > 0, 1440, 900);
        shell.GoTo(PageId.Workflows);
        Settle(window);

        var cards = window.GetVisualDescendants().OfType<RunCard>().Where(c => c.IsEffectivelyVisible).ToList();

        Assert.NotEmpty(cards);
        Assert.All(cards, c => Assert.Equal("AI agent", Named<TextBlock>(c, "WorkflowCardWho").Text));
    });
}
