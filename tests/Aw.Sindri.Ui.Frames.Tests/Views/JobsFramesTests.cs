using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using static Aw.Sindri.Ui.Tests.Jobs.JobsFixture;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Key frames for the Jobs page and its sheet (F3 jobs spec §7) at 1440 and 900 px wide, against the in-memory API serving the jobs goldens. Saved under frames/ as jobs-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class JobsFramesTests : FrameHost
{
    private const string Prefix = "jobs-";

    private async Task<(Window Window, ShellViewModel Shell)> OnJobs(double width, string scenario)
    {
        Server.JobsAnswer = RouteAnswer.Api("jobs", scenario);
        Server.JobAnswers[Id] = RouteAnswer.Api("jobs-id", "running");
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Jobs);
        await Until(window, () => shell.Jobs.Drawn > 0, "the Jobs page never read the list");
        return (window, shell);
    }

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_table_with_a_running_job(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnJobs(width, "running");

        Assert.Equal(shell.Jobs.Head, Named<TextBlock>(window, "JobsHead").Text);
        Assert.False(shell.Jobs.IsCompact);
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"table-{width}"), Prefix);
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_table_of_ended_jobs_says_nothing_runs(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnJobs(width, "by-kind");

        Assert.True(Visible(window, "JobsNoneRunningName"));
        Assert.True(shell.Jobs.IsNoneRunning);
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"ended-{width}"), Prefix);
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_running_sheet(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnJobs(width, "running");

        shell.Navigate(new DeepLink(DeepLinkKind.Job, Id));
        await Until(window, () => shell.JobSheet.Phase == FramePhase.Shown && shell.IsJobSheet);

        Assert.True(Visible(window, "JobIdCopy"));
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"sheet-{width}"), Prefix);
    });

    [Fact]
    public Task The_nothing_yet_state_offers_Services() => OnUi(async () =>
    {
        var (window, shell) = await OnJobs(1440, "empty-unfiltered");

        Assert.True(shell.Jobs.ShowServicesLink);
        Assert.True(Visible(window, "JobsServicesLink"));
        Frame(window, "empty", Prefix);
    });
}
