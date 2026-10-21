using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views;
using Aw.Sindri.Ui.Views.Setup;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>
/// Key frames of the setup checklist (setup spec §7), in Settings at 1440 and 900: a first run, a failed map, a setup that finished; and Home's one line. Rows are named for assistive tech,
/// the first section of Settings is the checklist, This app comes below it, and the page never scrolls sideways.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SetupFramesTests : FrameHost
{
    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnSettings(string scenario, double width)
    {
        Server.SetupAnswer = SetupFixture.Answer(scenario);
        var (window, shell) = await Connected(s => s.Setup.Steps.Count > 0, width, 900);
        window.SettingsSlot.Content = new SettingsView { DataContext = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), Motion, _ => { }, shell.Setup) };
        shell.GoTo(PageId.Settings);
        Settle(window);
        return (window, shell);
    }

    private static List<SetupStepRow> Rows(Window window) => [.. window.GetVisualDescendants().OfType<SetupStepRow>().Where(r => r.IsEffectivelyVisible)];

    private static Expander Expander(SetupStepRow row) => row.GetVisualDescendants().OfType<Expander>().First(e => string.Equals(e.Name, "SetupStepExpander", StringComparison.Ordinal));

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task A_first_run_shows_six_rows_the_first_open_and_never_scrolls_sideways(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnSettings("first-run", width);

        var rows = Rows(window);

        Assert.Equal(6, rows.Count);
        Assert.Equal([.. shell.Setup.Steps.Select(s => s.Name)], [.. rows.Select(r => Heard.Name(Expander(r)))], StringComparer.Ordinal);
        Assert.Equal([true, false, false, false, false, false], rows.Select(r => Expander(r).IsExpanded));
        Assert.Equal(("Set up Sindri", "0 of 6 done"), (Heard.Wrapper(Named<Border>(window, "SetupLeadName")), Heard.Wrapper(Named<Border>(window, "SetupCountName"))));
        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Content is not null && s.IsEffectivelyVisible && s.Extent.Height > 0);
        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 0.5, string.Create(CultureInfo.InvariantCulture, $"the page scrolls sideways: {scroll.Extent.Width} > {scroll.Viewport.Width}"));
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"setup-first-run-{width}"), prefix: "f3-");
    });

    [Fact]
    public Task Every_row_and_its_toggle_are_named_and_the_toggle_starts_with_the_step_title() => OnUi(async () =>
    {
        var (window, _) = await OnSettings("first-run", 1440);

        foreach (var row in Rows(window))
        {
            var title = ((ViewModels.Setup.SetupStepViewModel)row.DataContext!).Title;
            var heard = Heard.Tree(Expander(row));

            var toggle = heard.First(h => h.Type == Avalonia.Automation.Peers.AutomationControlType.Button);
            Assert.StartsWith(title, toggle.Name, StringComparison.Ordinal);
        }
    });

    [Fact]
    public Task The_checklist_is_the_first_section_and_This_app_comes_below_it() => OnUi(async () =>
    {
        var (window, _) = await OnSettings("first-run", 1440);

        var checklist = Named<Control>(window, "SetupChecklist");
        var title = Named<TextBlock>(window, "TitleText");
        var thisApp = Named<Border>(window, "ThisAppName");
        var switchRow = Named<ToggleSwitch>(window, "ReduceMotionSwitch");

        Assert.True(checklist.Bounds.Height > 0);
        Assert.True(checklist.TranslatePoint(default, window)!.Value.Y > title.TranslatePoint(default, window)!.Value.Y);
        Assert.True(thisApp.TranslatePoint(default, window)!.Value.Y >= checklist.TranslatePoint(new Point(0, checklist.Bounds.Height), window)!.Value.Y);
        Assert.True(switchRow.TranslatePoint(default, window)!.Value.Y > thisApp.TranslatePoint(default, window)!.Value.Y);
        Assert.Equal("This app", Heard.Name(thisApp));
    });

    [Fact]
    public Task A_failed_map_opens_with_its_sentence_and_one_Retry_and_the_others_stay_closed() => OnUi(async () =>
    {
        var (window, shell) = await OnSettings("failed", 1440);

        var rows = Rows(window);
        var map = rows.Single(r => ((ViewModels.Setup.SetupStepViewModel)r.DataContext!).Id == "map");

        Assert.Equal(["map"], rows.Where(r => Expander(r).IsExpanded).Select(r => ((ViewModels.Setup.SetupStepViewModel)r.DataContext!).Id), StringComparer.Ordinal);
        Assert.Equal("The last try at building the map didn't finish.", Named<Border>(map, "SetupStepError").Child is TextBlock t ? t.Text : null);
        var retry = map.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && string.Equals(Heard.Name(b), "Retry, Build the product map", StringComparison.Ordinal));
        Assert.True(retry.IsEnabled);
        Assert.Equal("Failed", shell.Setup.Steps.Single(s => s.Id == "map").ChipWord);
        Frame(window, "setup-failed-1440", prefix: "f3-");
    });

    [Fact]
    public Task A_protected_approval_row_shows_the_locked_terminal_copy_button() => OnUi(async () =>
    {
        Server.NeedsYouView = RouteAnswer.Api("needs-you", "empty");   // no profile approval row to open in the app: the button is the terminal copy
        var (window, _) = await OnSettings("profile-needs-approval", 1440);

        var profile = Rows(window).Single(r => ((ViewModels.Setup.SetupStepViewModel)r.DataContext!).Id == "profile");
        var locked = profile.GetVisualDescendants().OfType<Button>().Single(b => string.Equals(b.Name, "LockButton", StringComparison.Ordinal) && b.IsEffectivelyVisible);

        Assert.Equal("⌂ Review and approve · terminal", locked.Content);
    });

    [Fact]
    public Task A_finished_setup_says_so_and_its_rows_are_closed() => OnUi(async () =>
    {
        var (window, shell) = await OnSettings("all-done", 1440);

        Assert.Equal(("Sindri is set up", "All 6 done", false), (shell.Setup.Lead, shell.Setup.CountText, Visible(window, "SetupSubName")));
        Assert.All(Rows(window), r => Assert.False(Expander(r).IsExpanded));
        Frame(window, "setup-done-1440", prefix: "f3-");
    });

    [Fact]
    public Task Settings_without_the_route_says_setup_is_not_in_this_build_yet() => OnUi(async () =>
    {
        Server.SetupAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.NotFound, "SND-API-004", "The API answered without an error code");
        var (window, shell) = await Connected(s => s.Setup.IsNotServed, 1440, 900);
        window.SettingsSlot.Content = new SettingsView { DataContext = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), Motion, _ => { }, shell.Setup) };
        shell.GoTo(PageId.Settings);
        Settle(window);

        Assert.Equal("Setup isn't available in this build yet.", Heard.Wrapper(Named<Border>(window, "SetupNotServedName")));
        Assert.Empty(Rows(window));
    });

    [Fact]
    public Task Home_shows_one_line_that_opens_Settings_at_the_checklist() => OnUi(async () =>
    {
        Server.SetupAnswer = SetupFixture.Answer("profile-needs-approval");
        var (window, shell) = await Connected(s => s.Setup.IsHomeLineShown);
        shell.GoTo(PageId.Home);
        Settle(window);

        var line = Named<Button>(window, "SetupHomeLink");

        Assert.True(line.IsEffectivelyVisible);
        Assert.Equal("Finish setting up Sindri, 1 of 6 done. Next: Approve your settings", Heard.Name(line));
        var next = Named<Button>(window, "SetupHomeContinue");
        Assert.Equal(("Continue, Approve your settings", true), (Heard.Name(next), next.IsEffectivelyVisible));
        Assert.True(line.Bounds.Height <= 48, string.Create(CultureInfo.InvariantCulture, $"the line is {line.Bounds.Height} px high"));
        next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
        Assert.Equal((PageId.Settings, true), (shell.CurrentPage, shell.Setup.Steps.Single(s => s.Id == "profile").IsOpen));
    });

    [Fact]
    public Task Home_shows_no_line_once_setup_is_finished() => OnUi(async () =>
    {
        Server.SetupAnswer = SetupFixture.Answer("all-done");
        var (window, shell) = await Connected(s => s.Setup.IsFinished);
        shell.GoTo(PageId.Home);
        Settle(window);

        Assert.False(Visible(window, "SetupHomeLink") && Named<Button>(window, "SetupHomeLink").IsEffectivelyVisible);
    });
}
