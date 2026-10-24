using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The merge stop in a real window (needs-you spec, stop 3 as amended): a button per served verb, the merge in the Destructive style asking one line first, sent as the served desk action, and no terminal copy.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouVerbsViewTests : FrameHost
{
    private static readonly string[] _verbs = ["merge", "rework", "comments", "resync-docs", "check"];

    private async Task<(Window Window, ViewModels.Shell.ShellViewModel Shell)> MergeStop()
    {
        var instance = JsonNode.Parse(GoldenFiles.Api("workflow-instance", "dev-desk-round-trip"))!;
        foreach (var command in instance["current"]!["status"]!["commands"]!.AsArray().Where(c => c!["verb"]!.GetValue<string>() == "complete"))
        {
            command!["class"] = "protected";
        }

        instance["allowedCommands"] = new JsonArray(
            _verbs
                .Select(v => (JsonNode)new JsonObject { ["actionId"] = "ApplyCommand:command", ["cli"] = "sindri workflow command ITM-1/pr-babysitter/1 " + v, ["verb"] = v, ["destructive"] = string.Equals(v, "merge", StringComparison.Ordinal) ? true : null }).ToArray());
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "desk-task");
        Server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, instance.ToJsonString());
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, width: 1600, height: 900);
        shell.OpenNeedsYouInbox();
        Settle(window);
        await shell.Inbox.Bar.Loaded;
        Settle(window);
        return (window, shell);
    }

    private static List<Button> Verbs(Window window) =>
        [.. window.GetVisualDescendants().OfType<Button>().Where(b => string.Equals(b.Name, "NeedsYouVerb", StringComparison.Ordinal) && b.IsEffectivelyVisible)];

    [Fact]
    public Task The_merge_stop_has_a_button_per_verb_in_the_served_order_and_no_terminal_copy() => OnUi(async () =>
    {
        var (window, _) = await MergeStop();

        Assert.Equal(["Merge", "Send back to rework", "Held comments", "Resync docs", "Check now"],
            Verbs(window).Select(b => b.GetVisualDescendants().OfType<TextBlock>().Single().Text), StringComparer.Ordinal);
        Assert.Contains("destructive", Verbs(window)[0].Classes, StringComparer.Ordinal);
        Assert.All(Verbs(window).Skip(1), b => Assert.DoesNotContain("destructive", b.Classes, StringComparer.Ordinal));
        Assert.False(Visible(window, "NeedsYouTerminal"));
    });

    [Fact]
    public Task Pressing_Merge_asks_one_line_and_Confirm_sends_the_served_desk_action() => OnUi(async () =>
    {
        var (window, shell) = await MergeStop();

        Verbs(window)[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
        Assert.True(Visible(window, "NeedsYouVerbQuestion"));
        Assert.Equal("Merge this pull request? It cannot be undone.", Named<TextBlock>(window, "NeedsYouVerbQuestion").Text);
        Assert.Empty(Server.CommandCalls);

        Named<Button>(window, "NeedsYouVerbConfirm").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.Inbox.Bar.IsApproved);
        var (_, body) = Assert.Single(Server.CommandCalls);
        Assert.Equal((CommandKind.Command, "merge"), (body.Kind, body.Verb));
    });
}
