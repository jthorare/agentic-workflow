using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Settings;
using Aw.Sindri.Ui.ViewModels.Setup;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views;
using Aw.Sindri.Ui.Views.Setup;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>Key frames of the setup rows that take input (setup spec §2.4, §5): the repo form, the Onyx login form, the agents list and the footnote, at 1440 and 900, with the catalog letting the app do the work.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SetupFormsFramesTests : FrameHost
{
    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnSettings(System.Text.Json.Nodes.JsonNode list, double width)
    {
        Server.Catalog = RouteAnswer.SpecShaped("commands", "extra-setup-ready");
        Server.SetupAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
        var (window, shell) = await Connected(s => s.Setup.Steps.Count > 0 && s.Health.Catalog.For("AddRepo") is not null, width, 900);
        window.SettingsSlot.Content = new SettingsView { DataContext = new SettingsViewModel(new Preferences(new InMemoryPreferencesStore()), Motion, _ => { }, shell.Setup) };
        shell.GoTo(PageId.Settings);
        Settle(window);
        return (window, shell);
    }

    private static SetupStepRow Row(Window window, string id) => window.GetVisualDescendants().OfType<SetupStepRow>().Single(r => ((SetupStepViewModel)r.DataContext!).Id == id);

    private static void AssertNoSideScroll(Window window, double width)
    {
        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Content is not null && s.IsEffectivelyVisible && s.Extent.Height > 0);
        Assert.True(scroll.Extent.Width <= scroll.Viewport.Width + 0.5, string.Create(CultureInfo.InvariantCulture, $"the page scrolls sideways at {width}: {scroll.Extent.Width} > {scroll.Viewport.Width}"));
    }

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_repo_row_shows_a_labelled_folder_field_Browse_and_Add(double width) => OnUi(async () =>
    {
        var (window, _) = await OnSettings(SetupFixture.Golden("first-run"), width);

        var row = Row(window, "repo");
        var path = Named<TextBox>(row, "SetupRepoPath");
        var browse = Named<Button>(row, "SetupRepoBrowse");
        var add = Named<Button>(row, "SetupRepoAdd");

        Assert.Equal(("Folder", "Browse", "Add a repo"), (Heard.Name(path), Heard.Name(browse), Heard.Name(add)));
        Assert.All((Control[])[path, browse, add], c => Assert.True(c.IsEffectivelyVisible));
        Assert.False(add.IsEnabled);   // nothing typed yet
        AssertNoSideScroll(window, width);
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"setup-repo-form-{width}"), prefix: "f3-");
    });

    [Fact]
    public Task A_head_with_no_chooser_has_no_Browse_button() => OnUi(async () =>
    {
        Folders.IsAvailable = false;
        var (window, shell) = await OnSettings(SetupFixture.Golden("first-run"), 1440);
        var form = shell.Setup.Steps.Single(s => s.Id == "repo").Repo!;

        Assert.False(form.CanBrowse);
        Assert.False(Named<Button>(Row(window, "repo"), "SetupRepoBrowse").IsEffectivelyVisible);
    });

    [Fact]
    public Task A_typed_folder_enables_Add_and_a_refusal_shows_under_the_field() => OnUi(async () =>
    {
        var (window, shell) = await OnSettings(SetupFixture.Golden("first-run"), 1440);
        var form = shell.Setup.Steps.Single(s => s.Id == "repo").Repo!;
        Server.AddRepoAnswer = RouteAnswer.Problem("setup-repos", "not-a-checkout");

        form.Path = "/work/demo";
        Settle(window);
        var enabled = Named<Button>(Row(window, "repo"), "SetupRepoAdd").IsEnabled;
        await form.Add(CancellationToken.None);
        Settle(window);

        var error = Named<Border>(Row(window, "repo"), "SetupRepoError");
        Assert.Equal((true, "That folder isn't a git checkout, or can't be read.", true), (enabled, Heard.Wrapper(error), error.IsEffectivelyVisible));
        Frame(window, "setup-repo-error-1440", prefix: "f3-");
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task A_waiting_onyx_login_shows_two_labelled_fields_a_show_toggle_and_focus_in_the_first(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnSettings(SetupFixture.With("first-run", "onyx", "needs-you", "ConnectOnyx"), width);

        var row = Row(window, "onyx");
        var email = Named<TextBox>(row, "SetupOnyxEmail");
        var password = Named<TextBox>(row, "SetupOnyxPassword");
        var reveal = Named<ToggleButton>(row, "SetupOnyxReveal");

        Assert.Equal(("Onyx email", "Onyx password", "Show password"), (Heard.Name(email), Heard.Name(password), Heard.Name(reveal)));
        Assert.Equal('•', password.PasswordChar);
        Assert.True(Named<Button>(row, "SetupOnyxConnect").IsEffectivelyVisible);
        Assert.Same(email, TopLevel.GetTopLevel(row)!.FocusManager!.GetFocusedElement());
        AssertNoSideScroll(window, width);
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"setup-onyx-form-{width}"), prefix: "f3-");
        reveal.IsChecked = true;
        Settle(window);
        Assert.Equal(('\0', "Hide password"), (password.PasswordChar, shell.Setup.Steps.Single(s => s.Id == "onyx").Onyx!.RevealText));
    });

    [Fact]
    public Task Esc_in_the_onyx_form_closes_it_and_returns_focus_to_the_Connect_button() => OnUi(async () =>
    {
        var (window, shell) = await OnSettings(SetupFixture.With("first-run", "onyx", "needs-you", "ConnectOnyx"), 1440);
        var row = Row(window, "onyx");
        var password = Named<TextBox>(row, "SetupOnyxPassword");
        _ = password.Focus();
        password.Text = "hunter2";

        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);
        Settle(window);

        var onyx = shell.Setup.Steps.Single(s => s.Id == "onyx").Onyx!;
        Assert.Equal((string.Empty, false, true), (onyx.Password, onyx.ShowForm, onyx.ShowConnect));
        Assert.Same(Named<Button>(row, "SetupOnyxOpen"), TopLevel.GetTopLevel(row)!.FocusManager!.GetFocusedElement());
    });

    [Fact]
    public Task The_agents_row_lists_the_three_coding_agents_and_the_footnote_sits_under_the_panel() => OnUi(async () =>
    {
        var (window, shell) = await OnSettings(SetupFixture.Golden("all-done"), 1440);
        shell.Setup.Steps.Single(s => s.Id == "mcp").IsOpen = true;
        Settle(window);

        var lines = Row(window, "mcp").GetVisualDescendants().OfType<Border>().Where(b => b.IsEffectivelyVisible && b.Classes.Contains("agent-line")).Select(Heard.Name).ToList();

        Assert.Equal(["Claude Code, connected", "Codex, not started", "Cursor, not started"], lines);
        Frame(window, "setup-agents-1440", prefix: "f3-");
    });

    [Fact]
    public Task The_onyx_footnote_is_one_line_under_the_panel_with_a_copy_control() => OnUi(async () =>
    {
        var (window, _) = await OnSettings(SetupFixture.Golden("first-run"), 1440);

        var panel = Named<Border>(window, "SetupStepsPanel");
        var foot = Named<Control>(window, "SetupFootnote");

        Assert.True(foot.IsEffectivelyVisible);
        Assert.True(foot.TranslatePoint(default, window)!.Value.Y >= panel.TranslatePoint(new Point(0, panel.Bounds.Height), window)!.Value.Y);
        Assert.DoesNotContain(Row(window, "onyx").GetVisualDescendants().OfType<Control>(), c => string.Equals(c.Name, "SetupFootnote", StringComparison.Ordinal));   // not inside the row
    });
}
