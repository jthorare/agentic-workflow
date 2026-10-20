using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Jobs;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;
using static Aw.Sindri.Ui.Tests.Jobs.JobsFixture;

namespace Aw.Sindri.Ui.Tests.Jobs;

/// <summary>The Jobs page and the job sheet in the window: rows named for assistive tech, lines as named text, a sheet whose technical fields wait behind a closed Details, and System's link.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class JobsPageViewTests : FrameHost
{
    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnJobs()
    {
        Server.JobAnswers[Id] = RouteAnswer.Api("jobs-id", "running");
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Jobs);
        await Until(window, () => shell.Jobs.Drawn > 0, "the Jobs page never read the list");
        return (window, shell);
    }

    private static List<Border> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "JobRowFrame" && b.IsEffectivelyVisible)];

    [Fact]
    public Task The_page_has_a_row_per_job_named_in_words() => OnUi(async () =>
    {
        var (window, shell) = await OnJobs();

        var row = Assert.Single(Rows(window));
        var vm = (JobRowViewModel)row.DataContext!;
        Assert.Equal(vm.Name, Heard.Name(row));
        Assert.Equal(shell.Jobs.Head, Named<TextBlock>(window, "JobsHead").Text);
        Assert.Equal(shell.Jobs.HeadName, Heard.Wrapper(Named<Border>(window, "JobsHeadName")));
        Assert.Equal(vm.ChipText, Heard.Wrapper(Named<Border>(window, "JobRowChip")));
        Assert.DoesNotContain(Id, Heard.Name(row), StringComparison.Ordinal);
    });

    [Fact]
    public Task A_running_row_has_Cancel_that_sends_once() => OnUi(async () =>
    {
        var (window, shell) = await OnJobs();

        var cancel = Named<Button>(window, "RunButton");
        Assert.Equal("Cancel job Echo job", Heard.Name(cancel));
        cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.Jobs.Rows[0].Cancel!.IsAccepted, "the cancel never came back accepted");

        Assert.Equal([Id], Server.CancelledJobs, StringComparer.Ordinal);
    });

    [Fact]
    public Task The_empty_list_says_what_the_API_says() => OnUi(async () =>
    {
        Server.JobsAnswer = RouteAnswer.Api("jobs", "empty");
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Jobs);
        await Until(window, () => shell.Jobs.Drawn > 0, "the Jobs page never read the list");

        Assert.Equal(shell.Jobs.EmptyText, Heard.Wrapper(Named<Border>(window, "JobsEmptyName")));
        Assert.Empty(Rows(window));
    });

    [Fact]
    public Task A_title_click_opens_the_sheet_with_Details_closed() => OnUi(async () =>
    {
        var (window, shell) = await OnJobs();

        Named<Button>(window, "JobRowOpen").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => shell.JobSheet.Phase == FramePhase.Shown, "the job sheet never read the job");

        Assert.Equal((true, false), (Visible(window, "JobSheetContent"), Named<Expander>(window, "JobDetails").IsExpanded));
        Assert.Equal(shell.JobSheet.StateLine, Heard.Wrapper(Named<Border>(window, "JobState")));
        Assert.Equal("Job Echo job", Heard.Name(Named<Border>(window, "Sheet")));
    });

    [Fact]
    public Task System_leads_to_the_Jobs_page() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.GoTo(PageId.System);

        Named<Button>(window, "JobsLink").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(PageId.Jobs, shell.CurrentPage);
        await Until(window, () => shell.Jobs.Drawn > 0, "the Jobs page never read the list");
        Assert.Equal("Background jobs, opens the Jobs page", Heard.Name(Named<Button>(window, "JobsLink")));
    });
}
