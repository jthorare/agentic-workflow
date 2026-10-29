using Avalonia.Automation.Peers;
using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Projects page in the window (mockup B): group headings, cards named for assistive tech, the folds, the empty block and the banner.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ProjectsPageViewTests : FrameHost
{
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
    public Task Leading_nothing_shows_the_served_sentence_in_the_empty_block_with_a_disabled_Add() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects("empty-leads-nothing");

        Assert.Equal(shell.Projects.Sentence, Heard.Wrapper(Named<Border>(window, "ProjectsEmptyName")));
        Assert.Equal((true, false, true), (Visible(window, "ProjectsEmptyBlock"), Visible(window, "ProjectsBanner"), Visible(window, "ProjectsAddEmpty")));
        Assert.False(Named<Button>(window, "ProjectsAddEmpty").IsEnabled);
        Assert.Empty(Frames(window));
    });

    [Fact]
    public Task Syncing_shows_the_empty_block_without_Add() => OnUi(async () =>
    {
        var (window, _) = await OnProjects("syncing");

        Assert.Equal((true, false), (Visible(window, "ProjectsEmptyBlock"), Visible(window, "ProjectsAddEmpty")));
    });

    [Fact]
    public Task Context_down_shows_the_banner_over_cards_with_names_only() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects("context-down");

        Assert.Equal(shell.Projects.Sentence, Heard.Wrapper(Named<Border>(window, "ProjectsBannerName")));
        Assert.Equal((true, false), (Visible(window, "ProjectsBanner"), Visible(window, "ProjectsEmptyBlock")));
        Assert.Equal(2, Frames(window).Count);
        Assert.False(Visible(window, "ProjectCardStats"));
        Assert.False(Visible(window, "ProjectCardFooter"));
    });

    [Fact]
    public Task The_head_has_the_freshness_line_and_a_disabled_Add_project() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects();

        Assert.Equal(shell.Projects.Freshness, Named<TextBlock>(window, "ProjectsFreshness").Text);
        var add = Named<Button>(window, "ProjectsAdd");
        Assert.Equal(("+ Add project", false), (add.Content, add.IsEnabled));
    });

    private static ExpandCollapseState State(Expander fold) =>
        ControlAutomationPeer.CreatePeerForElement(fold).GetProvider<IExpandCollapseProvider>()!.ExpandCollapseState; // why: an Expander always has the provider
}
