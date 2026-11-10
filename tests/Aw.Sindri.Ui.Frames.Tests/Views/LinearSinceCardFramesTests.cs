using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Work > Linear "Since you last looked" card in the window at 900 px (#2536): the card header with its one action, grouped rows with a long title that trims, and the empty and error states.</summary>
[Lane(TestLane.Integration)]
public sealed class LinearSinceCardFramesTests : FrameHost
{
    private static string LongTitle => "Retry budget for the sync worker: cap the retries at five per hour, log each one, and stop paging the owner at night";

    private async Task<(Ui.Views.Shell.ShellWindow Window, ViewModels.Shell.ShellViewModel Shell)> OnLinear(string? since = null)
    {
        if (since is not null)
        {
            Server.LinearSinceAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, since);
        }

        var (window, shell) = await Connected(width: 900, height: 700);
        shell.GoTo(PageId.Work);
        shell.Linear.Choose(linear: true);
        await Until(window, () => Visible(window, "LinearStrip"), "the card never showed");
        return (window, shell);
    }

    private static string ThreeWithALongTitle()
    {
        var since = JsonNode.Parse(GoldenFiles.Api("linear-since", "three"))!.AsObject();   // why: the golden is a JSON object
        foreach (var item in since["items"]!.AsArray())
        {
            if (string.Equals(item!["key"]!.GetValue<string>(), "ENG-31", StringComparison.Ordinal))
            {
                item["title"] = LongTitle;
            }
        }

        return since.ToJsonString();
    }

    [Fact]
    public Task The_card_header_carries_the_count_and_one_Mark_all_seen_action_on_its_right() => OnUi(async () =>
    {
        var (window, _) = await OnLinear(ThreeWithALongTitle());

        var card = Named<Border>(window, "LinearStrip");
        var markAll = Named<Button>(window, "LinearStripMarkAllSeen");
        Assert.Equal("Since you last looked, 2 items", AutomationProperties.GetName(card));
        Assert.Equal("Mark all seen", markAll.Content);
        Assert.True(markAll.TranslatePoint(new Point(markAll.Bounds.Width, 0), card)!.Value.X <= card.Bounds.Width, "the action is inside the card");
        Assert.True(markAll.TranslatePoint(default, card)!.Value.X > card.Bounds.Width / 2, "the action sits at the right");
        Frame(window, "linear-since-900", "l2536-");
    });

    [Fact]
    public Task A_row_keeps_its_time_and_chips_inside_the_card_and_trims_a_long_title_at_900_px() => OnUi(async () =>
    {
        var (window, _) = await OnLinear(ThreeWithALongTitle());
        Settle(window);

        var card = Named<Border>(window, "LinearStrip");
        var rows = window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "LinearSinceRowFrame").ToList();
        Assert.Equal(2, rows.Count);
        foreach (var row in rows)
        {
            var right = row.TranslatePoint(new Point(row.Bounds.Width, 0), card)!.Value.X;
            Assert.True(right <= card.Bounds.Width + 0.5, "a row never runs past the card");
        }

        var title = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "LinearSinceRowTitle" && t.Text == LongTitle);
        Assert.Equal(TextTrimming.CharacterEllipsis, title.TextTrimming);
        Assert.True(title.TranslatePoint(new Point(title.Bounds.Width, 0), card)!.Value.X <= card.Bounds.Width, "the title ends inside the card");
        foreach (var time in window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "LinearSinceRowTime"))
        {
            Assert.True(time.IsEffectivelyVisible && time.Bounds.Width > 0, "the time shows on every row");
        }
    });

    [Fact]
    public Task Nothing_new_shows_the_empty_message_in_the_card_and_no_action() => OnUi(async () =>
    {
        var (window, _) = await OnLinear(GoldenFiles.Api("linear-since", "empty"));

        Assert.True(Visible(window, "LinearSinceNothing"));
        Assert.False(Visible(window, "LinearStripMarkAllSeen"));
        Frame(window, "linear-since-empty-900", "l2536-");
    });

    [Fact]
    public Task A_refused_key_shows_the_reason_and_one_Reconnect_button() => OnUi(async () =>
    {
        var mine = JsonNode.Parse(GoldenFiles.Api("linear-issues", "mine"))!.AsObject();   // why: the golden is a JSON object
        mine["feed"] = new JsonObject { ["state"] = "failing", ["lastSyncedAt"] = "2025-12-31T22:05:00.0000000Z", ["code"] = "SND-CONTEXT-071", ["reread"] = null };
        Server.LinearIssuesAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, mine.ToJsonString());
        var (window, _) = await OnLinear();

        Assert.True(Visible(window, "LinearStripReason"));
        Assert.Equal("Reconnect", Named<Button>(window, "LinearStripReconnect").Content);
        Assert.False(Visible(window, "LinearStripMarkAllSeen"));
        Frame(window, "linear-since-error-900", "l2536-");
    });

    [Fact]
    public Task Not_connected_is_one_card_as_wide_as_the_page_and_New_issue_is_disabled() => OnUi(async () =>
    {
        Server.LinearIssuesAnswer = RouteAnswer.Api("linear-issues", "not-connected");
        var (window, _) = await OnLinearConnect();

        var card = Named<Border>(window, "LinearConnect");
        var page = Named<StackPanel>(window, "LinearFilterBar").GetVisualAncestors().OfType<StackPanel>().First();
        Assert.True(card.Bounds.Width >= page.Bounds.Width - 1, "the card runs the page's width");
        Assert.False(Named<Button>(window, "CreateButton").IsEnabled);
        Frame(window, "linear-not-connected-900", "l2536-");
    });

    private async Task<(Ui.Views.Shell.ShellWindow Window, ViewModels.Shell.ShellViewModel Shell)> OnLinearConnect()
    {
        var (window, shell) = await Connected(width: 900, height: 700);
        shell.GoTo(PageId.Work);
        shell.Linear.Choose(linear: true);
        await Until(window, () => Visible(window, "LinearConnect"), "the connect card never showed");
        return (window, shell);
    }
}
