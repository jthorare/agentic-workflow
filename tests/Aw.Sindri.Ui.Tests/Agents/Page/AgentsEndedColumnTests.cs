using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>The ended table's first column is the live table's status column (#2693): both title columns start at the same x, whether the live status text is narrower or wider than the old fixed 150 px.</summary>
[Lane(TestLane.Integration)]
public sealed class AgentsEndedColumnTests : FrameHost
{
    private async Task<Window> Shown(bool wideLiveStatus)
    {
        var list = JsonNode.Parse(GoldenFiles.Api("sessions", "recently-ended"))!;
        if (wideLiveStatus)
        {
            // "Waiting on you" with the "estimated" tag beside it is wider than 150 px.
            var live = list["sessions"]![0]!.AsObject();
            live["stateSource"] = "transcript";
            live["estimated"] = true;
        }

        Server.SessionAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.HasEnded);
        Named<Button>(window, "AgentsEndedToggle").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
        return window;
    }

    private static double X(Visual visual, Window window) => visual.TranslatePoint(default, window)!.Value.X;   // why: both are in the window

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task The_ended_title_starts_where_the_live_title_does_with_a_live_status_narrower_or_wider_than_150(bool wideLiveStatus) => OnUi(async () =>
    {
        var window = await Shown(wideLiveStatus);

        var live = window.GetVisualDescendants().OfType<SessionRow>().First(r => r.IsEffectivelyVisible);
        var ended = window.GetVisualDescendants().OfType<EndedSessionRow>().First(r => r.IsEffectivelyVisible);
        var liveTitle = Named<Button>(live, "SessionRowOpen");
        var endedTitle = ended.GetVisualDescendants().OfType<TextBlock>().First(t => Grid.GetColumn(t) == 1 && t.IsEffectivelyVisible);

        Assert.InRange(Math.Abs(X(liveTitle, window) - X(endedTitle, window)), 0, 1);
        if (wideLiveStatus)
        {
            Assert.True(X(liveTitle, window) - X(Named<Control>(live, "SessionRowChip"), window) > 150, "the live status column is wider than 150 px in this case");
        }
    });
}
