using System.Globalization;
using Avalonia.Controls;
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

/// <summary>Key frames of the setup rows that take input (setup spec §2.4, §5): the repo form and the agents list, at 1440 and 900, with the catalog letting the app do the work.</summary>
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

    [Fact]
    public Task The_agents_row_lists_the_three_coding_agents() => OnUi(async () =>
    {
        var (window, shell) = await OnSettings(SetupFixture.Golden("all-done"), 1440);
        shell.Setup.Steps.Single(s => s.Id == "mcp").IsOpen = true;
        Settle(window);

        var lines = Row(window, "mcp").GetVisualDescendants().OfType<Border>().Where(b => b.IsEffectivelyVisible && b.Classes.Contains("agent-line")).Select(Heard.Name).ToList();

        Assert.Equal(["Claude Code, connected", "Codex, not started", "Cursor, not started"], lines);
        Frame(window, "setup-agents-1440", prefix: "f3-");
    });
}
