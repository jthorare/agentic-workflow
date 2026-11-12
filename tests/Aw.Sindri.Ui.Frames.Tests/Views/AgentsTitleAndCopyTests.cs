using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Two Agents page details (#2820, #2821): a live row's title text starts under the Agent header, and an ended row with no resume command shows no Copy button.</summary>
[Lane(TestLane.Integration)]
public sealed class AgentsTitleAndCopyTests : FrameHost
{
    [Fact]
    public Task Live_title_text_starts_under_the_Agent_header() => OnUi(async () =>
    {
        Server.SessionAnswer = RouteAnswer.Api("sessions", "fixture-three-providers");
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Groups.Count > 0);
        Settle(window);

        var head = Named<Grid>(window, "AgentsColumnsGrid").Children.OfType<Control>().ToList()[1];
        var row = window.GetVisualDescendants().OfType<SessionRow>().First(r => r.IsEffectivelyVisible);
        var text = Named<Button>(row, "SessionRowOpen").GetVisualDescendants().OfType<TextBlock>().First();
        var headX = head.TranslatePoint(default, window)!.Value.X;   // why: both are in the window
        var textX = text.TranslatePoint(default, window)!.Value.X;   // why: both are in the window

        Assert.True(Math.Abs(headX - textX) <= 1, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the title text starts at {textX} but the Agent header at {headX}"));
    });

    [Fact]
    public Task An_ended_row_with_no_resume_command_shows_no_Copy_button() => OnUi(async () =>
    {
        var list = JsonNode.Parse(GoldenFiles.Api("sessions", "recently-ended"))!;
        list["recentlyEnded"]![0]!.AsObject()["resume"] = null;
        Server.SessionAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.HasEnded);
        Named<Button>(window, "AgentsEndedToggle").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        var row = window.GetVisualDescendants().OfType<EndedSessionRow>().Single(r => r.IsEffectivelyVisible);
        var copy = Named<Button>(row, "EndedCopy");

        Assert.False(copy.IsEffectivelyVisible && copy.Opacity > 0, "a Copy button with nothing to copy is hidden, not shown disabled");
    });
}
