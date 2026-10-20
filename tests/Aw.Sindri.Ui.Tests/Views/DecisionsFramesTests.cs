using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// U3' key frames for decisions, requests and the row sheets, against P4's goldens: the decision sheet, an accepted outcome,
/// the two 409 refusals (decided elsewhere, voided) with focus kept on the control, a row cleared under an open sheet, the
/// item's requests and the failure and profile sheets. Saved under frames/ as u3-decision-*.png and friends.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class DecisionsFramesTests : FrameHost
{
    private const string Pending = "DEC-00000000000000000000000000000011";

    private async Task<(Window Window, ViewModels.Shell.ShellViewModel Shell)> WithDecisionOpen()
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "large");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, width: 1400);
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, Pending));
        Settle(window);
        return (window, shell);
    }

    private static Button Option(Window window, string label) =>
        Named<Control>(window, "DecisionSheet").GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("decision-option") && string.Equals((string?)b.Content, label, StringComparison.Ordinal));

    [Fact]
    public Task The_decision_sheet_shows_the_served_question_and_options() => OnUi(async () =>
    {
        var (window, _) = await WithDecisionOpen();

        var served = GoldenFiles.Node("decisions", "pending")["decisions"]![0]!;
        Assert.Equal((string)served["question"]!, Named<TextBlock>(window, "DecisionQuestion").Text);   // the served question (proof B4), not the row's title
        Assert.Equal(served["options"]!.AsArray().Select(o => (string)o!["label"]!), Named<Control>(window, "DecisionSheet").GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("decision-option")).Select(b => (string?)b.Content ?? ""), StringComparer.Ordinal);
        Frame(window, "decision-sheet");
        Frame(window, "decisions-pending");
    });

    [Fact]
    public Task An_accepted_decision_shows_what_was_decided() => OnUi(async () =>
    {
        var (window, shell) = await WithDecisionOpen();

        Option(window, "Accept").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await shell.Decision.Settled;
        Settle(window);

        Assert.Equal("Decided: Accept", Named<TextBlock>(window, "DecisionOutcome").Text);
        Frame(window, "decisions-decided");
    });

    [Fact]
    public Task Decided_elsewhere_409_shows_the_served_detail_and_keeps_focus_on_the_control() => OnUi(async () =>
    {
        Server.Decided = RouteAnswer.Problem("decisions-id", "decided-elsewhere");
        var (window, shell) = await WithDecisionOpen();
        var accept = Option(window, "Accept");
        Assert.True(accept.Focus(NavigationMethod.Tab));

        accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await shell.Decision.Settled;
        Settle(window);

        Assert.Equal("SND-ITEM-409", shell.Decision.RefusalCode);
        Assert.Equal((string)GoldenFiles.Node("decisions-id", "decided-elsewhere")["detail"]!, Named<TextBlock>(window, "DecisionRefusal").Text);
        Assert.Equal("accept", window.FocusManager?.GetFocusedElement() is Button { DataContext: DecisionOption focused } ? focused.Id : "", StringComparer.Ordinal); // why: the buttons are rebuilt, so the same option's new button
        Frame(window, "decision-409");
    });

    [Fact]
    public Task A_voided_decision_shows_its_served_refusal() => OnUi(async () =>
    {
        Server.Decided = RouteAnswer.Problem("decisions-id", "voided");
        var (window, shell) = await WithDecisionOpen();

        Option(window, "Decline").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await shell.Decision.Settled;
        Settle(window);

        Assert.Equal((string)GoldenFiles.Node("decisions-id", "voided")["detail"]!, Named<TextBlock>(window, "DecisionRefusal").Text);
        Frame(window, "decisions-voided");
    });

    [Fact]
    public Task A_row_cleared_under_the_open_sheet_shows_the_cleared_line() => OnUi(async () =>
    {
        var (window, shell) = await WithDecisionOpen();

        Server.NeedsYouView = RouteAnswer.Api("needs-you", "empty");
        shell.ShowNeedsYou(new NeedsYouFeed([], ""));
        await shell.Decision.Settled;
        Settle(window);

        Assert.True(shell.Decision.IsCleared);
        Assert.True(Visible(window, "DecisionCleared"));
        Frame(window, "decision-cleared");
    });

    [Fact]
    public Task The_item_sheet_draws_its_requests() => OnUi(async () =>
    {
        const string item = "ITM-0000000000000000000000000000000e";
        Server.DecisionsByItem[item] = RouteAnswer.Api("decisions", "by-item");
        Server.RequestsByItem[item] = RouteAnswer.Api("requests", "list-by-item");
        Server.Items[item] = Server.Items[A.NewItem];
        var (window, shell) = await Connected(width: 1400);

        shell.Navigate(new DeepLink(DeepLinkKind.Item, item));
        await Until(window, () => shell.Item.HasItem && shell.Item.Needs.Decisions.Count == 1);

        Assert.True(Visible(window, "ItemRequestList"));
        Frame(window, "requests");
    });

    [Fact]
    public Task The_failure_sheet_shows_its_details() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1400);
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Failure()], ""));

        shell.NavigateToRowSheet(new DeepLink(DeepLinkKind.NeedsYou, "failure:service:index"));
        Settle(window);

        Assert.True(Visible(window, "NeedsYouSheet"));
        Frame(window, "failure-sheet");
    });

    [Fact]
    public Task The_profile_sheet_keeps_the_hash_and_the_served_cli_behind_closed_Details() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1400);
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Profile(new NeedsYouSubject("profile", "3f1c09"))], ""));

        shell.NavigateToRowSheet(new DeepLink(DeepLinkKind.NeedsYou, "profile:drift"));
        Settle(window);

        var row = shell.NeedsYou.Rows.Single();
        Assert.False(Visible(window, "SheetRowCli"));
        Assert.True(Visible(window, "SheetRowMoreDetails"));   // the Details heading shows, closed
        Assert.False(row.DetailsOpen);
        var details = window.GetVisualDescendants().OfType<Expander>().Single(e => e.Name == "SheetRowMoreDetails");
        Assert.DoesNotContain(details.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && (t.Text == row.Cli || t.Text == "3f1c09"));
        Frame(window, "profile-sheet");

        row.DetailsOpen = true;
        Settle(window);

        Assert.Contains(details.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == row.Cli);
        Assert.Contains(details.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == "3f1c09");
    });
}
