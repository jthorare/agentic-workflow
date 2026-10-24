using System.Net;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The desk for the babysitter at ready, bound to the engine's own goldens (api #840): the verbs it serves are the buttons, and a step that serves none shows none.</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouServedVerbsViewTests : FrameHost
{
    private async Task<Window> DeskFor(string scenario, Func<string, string>? edit = null)
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "desk-task");
        Server.InstanceBody = _ => new RouteAnswer(HttpStatusCode.OK, (edit ?? (body => body))(GoldenFiles.Api("workflow-instance", scenario)));
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, width: 1600, height: 900);
        shell.OpenNeedsYouInbox();
        Settle(window);
        await shell.Inbox.Bar.Loaded;
        Settle(window);
        return window;
    }

    private static List<Button> Verbs(Window window) =>
        [.. window.GetVisualDescendants().OfType<Button>().Where(b => string.Equals(b.Name, "NeedsYouVerb", StringComparison.Ordinal) && b.IsEffectivelyVisible)];

    [Fact]
    public Task The_babysitter_at_ready_shows_its_five_served_verbs_in_order() => OnUi(async () =>
    {
        var window = await DeskFor("babysitter-ready-verbs");

        Assert.Equal(["Merge", "Send back to rework", "Held comments", "Resync docs", "Check now"],
            Verbs(window).Select(b => b.GetVisualDescendants().OfType<TextBlock>().Single().Text), StringComparer.Ordinal);
    });

    [Fact]
    public Task Merge_asks_for_confirmation_before_anything_is_sent() => OnUi(async () =>
    {
        var window = await DeskFor("babysitter-ready-verbs");

        var merge = Verbs(window)[0];
        Assert.Contains("destructive", merge.Classes, StringComparer.Ordinal);
        merge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.True(Visible(window, "NeedsYouVerbQuestion"));
        Assert.Empty(Server.CommandCalls);
    });

    [Fact]
    public Task A_verb_named_merge_without_the_served_flag_does_not_ask() => OnUi(async () =>
    {
        var window = await DeskFor("babysitter-ready-verbs", body => body.Replace(",\n      \"destructive\": true", "", StringComparison.Ordinal));

        var merge = Verbs(window)[0];
        Assert.DoesNotContain("destructive", merge.Classes, StringComparer.Ordinal);
        merge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.False(Visible(window, "NeedsYouVerbQuestion"));
    });

    [Fact]
    public Task A_verb_other_than_merge_with_the_served_flag_asks_for_confirmation() => OnUi(async () =>
    {
        var window = await DeskFor("babysitter-ready-verbs", body => body.Replace("\"verb\": \"rework\"", "\"verb\": \"rework\",\n      \"destructive\": true", StringComparison.Ordinal));

        var rework = Verbs(window)[1];
        Assert.Contains("destructive", rework.Classes, StringComparer.Ordinal);
        rework.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => string.Equals(t.Name, "NeedsYouVerbQuestion", StringComparison.Ordinal) && t.IsEffectivelyVisible);
        Assert.Empty(Server.CommandCalls);
    });

    [Fact]
    public Task A_step_that_serves_no_verbs_shows_no_verb_buttons() => OnUi(async () =>
    {
        var window = await DeskFor("dev-desk-round-trip");

        Assert.Empty(Verbs(window));
    });
}
