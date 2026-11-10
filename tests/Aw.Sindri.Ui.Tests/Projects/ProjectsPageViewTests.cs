using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using static Aw.Sindri.Ui.Tests.Projects.ProjectsFixture;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Projects page in the window (mockup B): group headings, cards named for assistive tech, the folds, the empty block and the banner.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ProjectsPageViewTests : FrameHost
{
    // The one Add project rule: the window shows exactly one visible button with the Add text.
    private static int AddButtons(ShellWindow window, ShellViewModel shell) =>
        window.GetVisualDescendants().OfType<Button>().Count(b => b.IsEffectivelyVisible && string.Equals(b.Content as string, shell.Projects.AddText, StringComparison.Ordinal));

    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnProjects(string? scenario = null)
    {
        if (scenario is not null)
        {
            Server.ProjectsAnswer = RouteAnswer.Api("projects", scenario);
        }

        var (window, shell) = await Connected();
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading, "the Projects page never read the list");
        return (window, shell);
    }

    private static List<Border> Frames(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "ProjectCardFrame" && b.IsEffectivelyVisible)];

    [Fact]
    public Task The_mixed_list_draws_a_heading_per_state_and_a_card_per_project() => OnUi(async () =>
    {
        var (window, _) = await OnProjects();

        var headings = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == "ProjectsGroupHeading" && t.IsEffectivelyVisible).Select(t => t.Text);
        Assert.Equal(["IN PROGRESS", "PLANNED", "PAUSED"], headings, StringComparer.Ordinal);
        Assert.Equal(["Checkout rewrite", "Kitchen", "Mobile app"], Frames(window).Select(f => ((ViewModels.Projects.ProjectCardViewModel)f.DataContext!).Name), StringComparer.Ordinal);
    });

    [Fact]
    public Task A_card_is_named_in_words_and_can_take_focus() => OnUi(async () =>
    {
        var (window, _) = await OnProjects();

        var card = Frames(window)[0];
        Assert.Equal("Checkout rewrite, In progress, at risk, 12 open issues", Heard.Name(card));
        Assert.True(card.Focusable);
    });

    [Fact]
    public Task The_Finished_fold_is_collapsed_until_toggled_and_says_so() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects();
        var fold = Named<Expander>(window, "ProjectsFinished");

        Assert.Equal((false, 3, ExpandCollapseState.Collapsed), (fold.IsExpanded, Frames(window).Count, State(fold)));

        shell.Projects.Finished.Toggle();
        await Until(window, () => Frames(window).Count == 4, "the Finished card never showed");

        Assert.Equal((true, ExpandCollapseState.Expanded), (fold.IsExpanded, State(fold)));
        Assert.Contains(Frames(window), f => ((ViewModels.Projects.ProjectCardViewModel)f.DataContext!).Name == "Search relaunch");
    });

    [Fact]
    public Task A_fold_with_no_cards_is_not_drawn() => OnUi(async () =>
    {
        var (window, _) = await OnProjects("syncing");

        Assert.False(Visible(window, "ProjectsFinished"));
        Assert.False(Visible(window, "ProjectsHidden"));
    });

    [Fact]
    public Task Leading_nothing_shows_the_served_sentence_in_the_empty_block_with_no_second_Add_project() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects("empty-leads-nothing");

        Assert.Equal(shell.Projects.Sentence, Heard.Wrapper(Named<Border>(window, "ProjectsEmptyName")));
        Assert.Equal((true, false, true, 1), (Visible(window, "ProjectsEmptyCard"), Visible(window, "ProjectsBanner"), Visible(window, "ProjectsAdd"), AddButtons(window, shell)));
        Assert.Empty(Frames(window));
    });

    [Fact]
    public Task Linear_not_connected_offers_Connect_Linear_in_the_empty_card_and_one_Add_project() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects("empty-no-linear");

        Assert.Equal((true, true, 1), (Visible(window, "ProjectsConnectEmpty"), Visible(window, "ProjectsAdd"), AddButtons(window, shell)));
        Assert.Equal("Connect Linear", Named<Button>(window, "ProjectsConnectEmpty").Content);
    });

    [Fact]
    public Task The_empty_card_has_a_solid_border_like_the_other_pages() => OnUi(async () =>
    {
        var (window, _) = await OnProjects("empty-no-linear");

        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>(), r => r.IsEffectivelyVisible && r.StrokeDashArray is { Count: > 0 });
        Assert.Equal(new Avalonia.Thickness(1), Named<Border>(window, "ProjectsEmptyCard").BorderThickness);
    });

    [Fact]
    public Task A_missing_key_banner_has_a_Connect_Linear_button_and_no_terminal_command() => OnUi(async () =>
    {
        var list = Golden("mixed");
        list["empty"] = "Linear needs a token. Run sindri index connect linear in your terminal to connect it.";
        Server.ProjectsAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading, "the Projects page never read the list");

        Assert.Equal((true, true), (Visible(window, "ProjectsBanner"), Visible(window, "ProjectsConnectBanner")));
        var words = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text ?? string.Empty);
        Assert.DoesNotContain(words, w => w.Contains("sindri index connect", StringComparison.Ordinal));
    });

    [Fact]
    public Task Context_down_shows_the_banner_over_cards_with_names_only() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects("context-down");

        Assert.Equal(shell.Projects.Sentence, Heard.Wrapper(Named<Border>(window, "ProjectsBannerName")));
        Assert.Equal((true, false), (Visible(window, "ProjectsBanner"), Visible(window, "ProjectsEmptyCard")));
        Assert.Equal(2, Frames(window).Count);
        Assert.False(Visible(window, "ProjectCardStats"));
        Assert.False(Visible(window, "ProjectCardFooter"));
    });

    [Fact]
    public Task The_head_has_the_freshness_line_and_an_enabled_Add_project() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects();

        Assert.Equal(shell.Projects.Freshness, Named<TextBlock>(window, "ProjectsFreshness").Text);
        var add = Named<Button>(window, "ProjectsAdd");
        Assert.Equal(("+ Add project", true), (add.Content, add.IsEnabled));
    });

    [Fact]
    public Task With_no_card_left_focus_goes_to_Add_project() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects("empty-no-linear");

        await shell.Projects.Settled(new ViewModels.Projects.ProjectDoneEventArgs(ViewModels.Projects.ProjectDone.Removed, "prj_00000000000000000000000008", "Mobile app removed", wentToFinished: false), Token);

        await Until(window, () => Named<Button>(window, "ProjectsAdd").IsFocused, "focus never reached Add project");
    });

    [Fact]
    public Task Esc_on_the_Remove_question_keeps_the_sheet_open_and_focus_on_Remove() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects();
        shell.Navigate(new DeepLink(DeepLinkKind.Project, "prj_00000000000000000000000008"));
        await Until(window, () => shell.ProjectSheet.IsShown && shell.ProjectSheet.CanRemove, "the sheet never offered Remove");
        shell.ProjectSheet.AskRemove();
        await Until(window, () => Named<Button>(window, "ProjectKeep").IsFocused, "focus never reached Keep");

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle(window);

        Assert.Equal((true, false), (shell.IsSheetOpen, shell.ProjectSheet.IsConfirmingRemove));
        await Until(window, () => Named<Button>(window, "ProjectRemove").IsFocused, "focus never went back to Remove");
    });

    private static ExpandCollapseState State(Expander fold) =>
        ControlAutomationPeer.CreatePeerForElement(fold).GetProvider<IExpandCollapseProvider>()!.ExpandCollapseState; // why: an Expander always has the provider
}
