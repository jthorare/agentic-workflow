using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>Key frames (UI spec §18.2: one per state, integration lane) for the Projects page, against the in-memory API serving the project goldens. Saved under frames/ as projects-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ProjectsFramesTests : FrameHost
{
    private async Task<(Window Window, ShellViewModel Shell)> OnProjects(string scenario, double width, double height)
    {
        Server.ProjectsAnswer = RouteAnswer.Api("projects", scenario);
        var (window, shell) = await Connected(width: width, height: height);
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading, "the Projects page never read the list");
        Settle(window);
        return (window, shell);
    }

    private static void Shown(Window window, ShellViewModel shell)
    {
        Assert.True(shell.IsProjects);
        Assert.True(Visible(window, "ProjectsRoot"));
    }

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(1000, 700)]
    public Task The_mixed_list_at(int width, int height) => OnUi(async () =>
    {
        var (window, shell) = await OnProjects("mixed", width, height);

        Shown(window, shell);
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"projects-mixed-{width}"), prefix: string.Empty);
    });

    [Fact]
    public Task The_Finished_fold_open() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects("mixed", 1440, 1200);   // tall enough to show the open fold whole
        shell.Projects.Finished.Toggle();
        Settle(window);

        Shown(window, shell);
        Assert.True(Named<Expander>(window, "ProjectsFinished").IsExpanded);
        Frame(window, "projects-finished-1440", prefix: string.Empty);
    });

    [Fact]
    public Task Context_down_shows_the_banner_and_names_only() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects("context-down", 1440, 900);

        Shown(window, shell);
        Assert.True(Visible(window, "ProjectsBanner"));
        Frame(window, "projects-context-down-1440", prefix: string.Empty);
    });

    [Fact]
    public Task Leading_nothing_shows_the_empty_block() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects("empty-leads-nothing", 1440, 900);

        Shown(window, shell);
        Assert.True(Visible(window, "ProjectsEmptyBlock"));
        Frame(window, "projects-empty-leads-nothing-1440", prefix: string.Empty);
    });

    [Fact]
    public Task A_cards_notes_open_for_editing() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects("mixed", 1440, 900);
        shell.Projects.Groups[0].Cards[0].BeginEdit();
        await Until(window, () => Visible(window, "ProjectNotesBox"), "the notes box never showed");
        Settle(window);

        Shown(window, shell);
        Frame(window, "projects-notes-edit-1440", prefix: string.Empty);
    });

    [Theory]
    [InlineData(1440, 900)]
    [InlineData(1000, 700)]
    public Task A_cards_sheet_at(int width, int height) => OnUi(async () =>
    {
        var (window, shell) = await OnProjects("mixed", width, height);
        shell.Navigate(new DeepLink(DeepLinkKind.Project, "prj_0000000000000000000000000H"));
        await Until(window, () => shell.ProjectSheet.IsShown, "the sheet never showed");
        Settle(window);

        Assert.True(shell.IsProjectSheet);
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"projects-sheet-{width}"), prefix: string.Empty);
    });

    [Fact]
    public Task The_sheet_asking_to_remove() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects("mixed", 1440, 900);
        shell.Navigate(new DeepLink(DeepLinkKind.Project, "prj_0000000000000000000000000H"));
        await Until(window, () => shell.ProjectSheet.IsShown && shell.ProjectSheet.CanRemove, "the sheet never offered Remove");
        shell.ProjectSheet.AskRemove();
        Settle(window);

        Assert.True(Visible(window, "ProjectRemoveAsk"));
        Frame(window, "projects-sheet-remove-1440", prefix: string.Empty);
    });

    [Fact]
    public Task Before_the_first_answer_the_page_shows_card_shaped_placeholders() => OnUi(async () =>
    {
        var gate = new TaskCompletionSource();
        Server.ProjectsGate = gate;
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Projects);
        Settle(window);

        Shown(window, shell);
        Assert.True(shell.Projects.IsLoading);
        Assert.True(Visible(window, "ProjectsLoading"));
        Frame(window, "projects-loading-1440", prefix: string.Empty);
        gate.SetResult();
    });
}
