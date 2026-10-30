using System.Text.Json.Nodes;
using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Needs you card's why section and its two outcome boxes in a real window (needs-you spec §4): the served why and what approving and sending back do, hidden when the recipe says nothing.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouWhyViewTests : FrameHost
{
    // The desk task, waiting in a state of the f5-fields recipe.
    private async Task<(ShellWindow Window, ViewModels.Shell.ShellViewModel Shell)> DeskIn(string state)
    {
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"))!;
        instance["recipe"]!["id"] = "workflow.f5-fields";
        instance["current"]!["state"] = state;
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "desk-task");
        Server.InstanceBody = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, instance.ToJsonString());
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, width: 1440, height: 900);
        shell.OpenNeedsYouInbox();
        Settle(window);
        await shell.Inbox.Bar.Loaded;
        Settle(window);
        return (window, shell);
    }

    [Fact]
    public Task The_approve_state_shows_its_why_and_both_boxes_with_their_words() => OnUi(async () =>
    {
        var (window, _) = await DeskIn("approve");

        Assert.Equal("You decide whether it goes on.", Named<TextBlock>(window, "NeedsYouWhyText").Text);
        Assert.True(Visible(window, "NeedsYouIfApprove") && Visible(window, "NeedsYouIfSendBack"));
        Assert.Equal("Moves on to the hand-over.", Named<TextBlock>(window, "NeedsYouIfApproveText").Text);
        Assert.Equal("Sends your notes back to the AI.", Named<TextBlock>(window, "NeedsYouIfSendBackText").Text);
    });

    [Fact]
    public Task A_state_with_no_outcome_text_hides_both_boxes_and_keeps_the_rows_reason() => OnUi(async () =>
    {
        var (window, shell) = await DeskIn("check");

        Assert.False(Visible(window, "NeedsYouIfApprove") || Visible(window, "NeedsYouIfSendBack"));
        Assert.Equal(shell.Inbox.Card!.Entry.Reason, Named<TextBlock>(window, "NeedsYouWhyText").Text);
    });
}
