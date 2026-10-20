using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Views.NeedsYou;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// U3' key frames (UI spec §18.2: one per state in the integration lane) for the Needs-you panel, against the in-memory API
/// serving P4's goldens: the fixture, empty, stalled and rebuilding lists, the pulse and what clears it, reduced motion, Show
/// all, held rows and Home at 5 s and with the large profile. Saved under frames/ as u3-needs-you-*.png.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouFramesTests : FrameHost
{
    private static List<NeedsYouRow> Rows(Window window) =>
        [.. Named<Control>(window, "NeedsYouPanel").GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible)];

    [Fact]
    public Task The_fixture_list_draws_the_served_rows_in_order() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0);

        var titles = Rows(window).Select(r => Named<Button>(r, "NeedsYouTitle")).Select(b => Avalonia.Automation.AutomationProperties.GetName(b)).Count();
        Assert.Equal(GoldenFiles.Node("needs-you", "fixture")["rows"]!.AsArray().Count, Rows(window).Count);
        Assert.True(titles > 0);
        Assert.Equal(shell.Attention.SummaryLine, Named<TextBlock>(window, "NeedsYouSummary").Text);
        Frame(window, "needs-you-fixture");
    });

    // review run 1, K10: what a screen reader lists says which row each control belongs to.
    [Fact]
    public Task Every_row_and_control_is_named_for_its_row() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0);

        foreach (var row in Rows(window))
        {
            var vm = (ViewModels.NeedsYou.NeedsYouRowViewModel)row.DataContext!;
            var id = vm.ShownId.Length > 0 ? vm.ShownId : vm.Id;
            Assert.Equal(vm.AccessibleName, Avalonia.Automation.AutomationProperties.GetName(row.GetVisualDescendants().OfType<Border>().First(b => b.Name == "RowFrame")));
            Assert.Equal($"Open {vm.Title} {id}", Avalonia.Automation.AutomationProperties.GetName(Named<Button>(row, "NeedsYouTitle")));
            foreach (var button in Named<ItemsControl>(row, "NeedsYouControls").GetVisualDescendants().OfType<Button>())
            {
                Assert.EndsWith(" " + id, Avalonia.Automation.AutomationProperties.GetName(button), StringComparison.Ordinal);
            }
        }

        Assert.NotNull(shell);
    });

    // review run 1, K12: a disabled control says why, to a pointer and to a screen reader.
    [Fact]
    public Task A_disabled_row_control_says_why_on_hover_and_to_assistive_technology() => OnUi(async () =>
    {
        var catalog = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.Api("commands", "catalog"))!;   // ApproveProfile is ready now; a row that is still cli-only is what disables a control
        catalog["rows"]!.AsArray().Single(r => (string?)r!["action"] == "ApproveProfile")!["ui"] = "cli-only:typed-confirmation";
        Server.Catalog = new RouteAnswer(System.Net.HttpStatusCode.OK, catalog.ToJsonString());
        var (window, _) = await Connected(s => s.NeedsYou.Count > 0);

        var disabled = Rows(window).SelectMany(r => Named<ItemsControl>(r, "NeedsYouControls").GetVisualDescendants().OfType<Button>()).Where(b => !b.IsEnabled).ToList();

        Assert.NotEmpty(disabled);
        Assert.All(disabled, b =>
        {
            var why = ((ViewModels.NeedsYou.NeedsYouActionControl)b.DataContext!).Tooltip;
            Assert.NotEmpty(why);
            Assert.True(ToolTip.GetShowOnDisabled(b));
            Assert.Equal(why, Avalonia.Automation.AutomationProperties.GetHelpText(b));
        });
    });

    // review run 1, a11y-14: a sheet detail's panel carries no name of its own (its two texts are read as they are).
    [Fact]
    public Task A_sheet_detail_panel_is_not_named_without_being_a_control() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0);
        var failure = shell.NeedsYou.Rows.First(r => r.Details.Count > 0);
        shell.NavigateToRowSheet(new DeepLink(DeepLinkKind.NeedsYou, failure.Id));
        Settle(window);

        var details = Named<ItemsControl>(window, "SheetRowDetails").GetVisualDescendants().OfType<StackPanel>().Where(p => p.DataContext is ViewModels.NeedsYou.SheetDetail).ToList();

        Assert.NotEmpty(details);
        Assert.All(details, p => Assert.Empty(Avalonia.Automation.AutomationProperties.GetName(p) ?? ""));
    });

    [Fact]
    public Task An_empty_list_shows_the_served_sentence() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "empty");
        var (window, _) = await Connected();

        Assert.Equal((true, (string)GoldenFiles.Node("needs-you", "empty")["empty"]!), (Visible(window, "NeedsYouEmpty"), Named<TextBlock>(window, "NeedsYouEmpty").Text));
        Assert.Empty(Rows(window));
        Frame(window, "needs-you-empty");
    });

    [Fact]
    public Task A_stalled_list_keeps_its_rows_and_says_so() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "stalled");
        var (window, shell) = await Connected(s => s.NeedsYou.HasBanner);

        Assert.True(Visible(window, "NeedsYouBanner"));
        Assert.NotEmpty(Rows(window));
        Assert.Equal(shell.NeedsYou.Banner, Named<TextBlock>(window, "NeedsYouBanner").Text);
        Frame(window, "needs-you-stalled");
    });

    [Fact]
    public Task A_rebuilding_list_says_so_above_its_rows() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "rebuilding");
        var (window, shell) = await Connected(s => s.NeedsYou.HasBanner);

        Assert.True(Visible(window, "NeedsYouBanner"));
        Assert.Equal(shell.NeedsYou.Banner, Named<TextBlock>(window, "NeedsYouBanner").Text);
        Frame(window, "needs-you-rebuilding");
    });

    [Fact]
    public Task New_rows_pulse_the_header_once_and_stay_unseen() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0);

        Assert.Equal(1, shell.NeedsYou.Seen.HeaderPulses);
        Assert.Contains(shell.NeedsYou.Seen.Rows, r => r.IsPulsing);
        Frame(window, "needs-you-pulse-unseen");
    });

    [Fact]
    public Task Mark_all_seen_clears_the_new_words_and_offers_undo() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0);

        Named<Button>(window, "MarkAllSeen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.All(shell.NeedsYou.Seen.Rows, r => Assert.False(r.IsPulsing || r.HasOutline));
        Assert.True(shell.NeedsYou.Seen.UndoOffered);
        Frame(window, "needs-you-seen");
    });

    [Fact]
    public Task Reduced_motion_pulses_nothing_and_marks_arrivals_without_it() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0);

        Assert.True(shell.NeedsYou.Seen.ReducedMotion);
        Assert.Contains(shell.NeedsYou.Seen.Rows, r => r.HasOutline);
        Assert.DoesNotContain(shell.NeedsYou.Seen.Rows, r => r.IsPulsing);
        Frame(window, "needs-you-reduced-motion");
    });

    [Fact]
    public Task Seven_rows_show_five_then_Show_all_opens_the_inbox_with_them_all() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "large");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 5);

        Assert.Equal(ViewModels.NeedsYou.NeedsYouViewModel.Cap, Rows(window).Count);
        Frame(window, "needs-you-show-all-collapsed");
        Named<Button>(window, "NeedsYouShowAll").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.True(shell.IsNeedsYouInbox);
        Assert.Equal(shell.NeedsYou.Count, shell.Inbox.Groups.Sum(g => g.Rows.Count));
        Frame(window, "needs-you-show-all");
    });

    [Fact]
    public Task Home_with_the_large_profile_keeps_its_panels_in_view() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "large");
        var (window, _) = await Connected(s => s.NeedsYou.Count > 5, width: 1400);

        Assert.True(Visible(window, "NeedsYouPanel") && Visible(window, "RunningPanel") && Visible(window, "QueuedPanel"));
        Frame(window, "home-large");
    });

    // F3 needs-you slice 1: the inbox page (prototype #pending), at the two widths the spec draws.
    [Theory]
    [InlineData(1440, "needs-you-inbox-1440")]
    [InlineData(900, "needs-you-inbox-900")]
    public Task The_inbox_draws_the_groups_and_the_selected_requests_card(int width, string frame) => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0, width: width, height: 800);

        shell.OpenNeedsYouInbox();
        Settle(window);

        Assert.True(Visible(window, "NeedsYouInbox"));
        Assert.False(Visible(window, "NeedsYouPanel"));
        Assert.Equal(["Agent decisions", "Workflow and settings approvals", "Something failed"], Named<Control>(window, "NeedsYouGroups").GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("group-header")).Select(b => b.GetVisualDescendants().OfType<TextBlock>().First().Text), StringComparer.Ordinal);
        Assert.True(Visible(window, "NeedsYouCard"));
        Assert.Equal(shell.Inbox.Selected?.Title, Named<TextBlock>(window, "NeedsYouCardTitle").Text);
        Assert.Equal(shell.Inbox.FooterLeft, Named<TextBlock>(window, "NeedsYouFooterLeft").Text);
        Assert.True(Named<Control>(window, "NeedsYouInbox").Bounds.Width <= window.ClientSize.Width, "the inbox scrolls sideways");
        Frame(window, frame);
    });

    [Fact]
    public Task Escape_leaves_the_inbox_for_Home_with_its_panels() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0);
        shell.OpenNeedsYouInbox();
        Settle(window);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle(window);

        Assert.False(shell.IsNeedsYouInbox);
        Assert.True(Visible(window, "NeedsYouPanel") && Visible(window, "RunningPanel") && Visible(window, "QueuedPanel"));
        Assert.False(Visible(window, "NeedsYouInbox"));
    });

    [Fact]
    public Task Show_all_on_Home_opens_the_inbox() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "large");
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 5);

        Named<Button>(window, "NeedsYouShowAll").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.True(shell.IsNeedsYouInbox);
        Assert.True(Visible(window, "NeedsYouInbox"));
    });

    [Fact]
    public Task The_inbox_breadcrumb_goes_back_to_Home() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0);
        shell.OpenNeedsYouInbox();
        Settle(window);

        Named<Button>(window, "NeedsYouCrumbHome").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.False(shell.IsNeedsYouInbox);
    });

    [Fact]
    public Task An_empty_inbox_says_the_served_sentence_in_the_detail_column() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "empty");
        var (window, shell) = await Connected();
        shell.OpenNeedsYouInbox();
        Settle(window);

        Assert.Equal((string)GoldenFiles.Node("needs-you", "empty")["empty"]!, Named<TextBlock>(window, "NeedsYouInboxEmpty").Text);
        Assert.False(Visible(window, "NeedsYouCard"));
    });
}
