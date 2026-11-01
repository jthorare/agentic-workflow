using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Repository cell of an ended row (agents spec §2, #1405): the repository when the session had one, the dash placeholder when it had none, as a live row shows since #1384.</summary>
[Lane(TestLane.Integration)]
public sealed class AgentsEndedRepoCellTests : FrameHost
{
    private async Task<string?> EndedRepoCell(Action<JsonObject> edit)
    {
        var list = JsonNode.Parse(GoldenFiles.Api("sessions", "recently-ended"))!;
        edit(list["recentlyEnded"]![0]!["session"]!.AsObject());
        Server.SessionAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.HasEnded);
        Named<Button>(window, "AgentsEndedToggle").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
        var row = Assert.Single(window.GetVisualDescendants().OfType<EndedSessionRow>(), r => r.IsEffectivelyVisible);
        return Named<TextBlock>(row, "EndedRepo").Text;
    }

    [Fact]
    public Task An_ended_session_with_no_repository_shows_the_dash_in_the_Repository_cell() => OnUi(async () =>
        Assert.Equal(Labels.EmptyCell, await EndedRepoCell(session => session["repo"] = null)));

    [Fact]
    public Task An_ended_session_with_a_repository_shows_that_repository_in_the_Repository_cell() => OnUi(async () =>
        Assert.Equal("api", await EndedRepoCell(session => session["repo"] = "api")));
}
