using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The merge stop's pull request card in a real window (needs-you spec, stop 3): the link and the four lines show on the card, and a request with no status shows none.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouMergeCardViewTests : FrameHost
{
    private async Task<Window> MergeInbox(bool listed)
    {
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"))!;
        foreach (var command in instance["current"]!["status"]!["commands"]!.AsArray().Where(c => c!["verb"]!.GetValue<string>() == "complete"))
        {
            command!["class"] = "protected";
        }

        if (listed)
        {
            var row = JsonNode.Parse(instance["facts"]![0]!.ToJsonString())!;
            row["key"] = "fact:prStatus";
            row["type"] = "payload";
            instance["facts"]!.AsArray().Add(row);
        }

        Server.NeedsYouView = RouteAnswer.Api("needs-you", "desk-task");
        Server.InstanceBody = _ => new RouteAnswer(System.Net.HttpStatusCode.OK, instance.ToJsonString());
        Server.FactBody = (_, key) => string.Equals(key, "fact:prStatus", StringComparison.Ordinal) ? new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.Api("workflow-instance-fact", "pr-status")) : null;
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, width: 1440, height: 900);
        shell.OpenNeedsYouInbox();
        Settle(window);
        await shell.Inbox.Bar.Loaded;
        Settle(window);
        return window;
    }

    [Fact]
    public Task The_merge_stop_card_shows_the_pull_request_and_its_four_lines() => OnUi(async () =>
    {
        var window = await MergeInbox(listed: true);

        Assert.True(Visible(window, "NeedsYouMergeCard"));
        Assert.Equal("#12", Assert.IsType<Button>(Named<Button>(window, "NeedsYouMergePr")).Content);
        var texts = Named<ItemsControl>(window, "NeedsYouMergeLines").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("5 passed, 2 failed, 1 pending", texts, StringComparer.Ordinal);
        Assert.Contains("1 open, 2 resolved", texts, StringComparer.Ordinal);
        Assert.Contains("Proof not read yet", texts, StringComparer.Ordinal);
        Assert.Contains("Missing", texts, StringComparer.Ordinal);
    });

    [Fact]
    public Task A_request_with_no_status_has_no_card() => OnUi(async () =>
    {
        var window = await MergeInbox(listed: false);

        Assert.False(Visible(window, "NeedsYouMergeCard"));
    });
}
