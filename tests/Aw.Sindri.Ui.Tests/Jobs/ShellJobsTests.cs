using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Ui.Tests.Jobs.JobsFixture;

namespace Aw.Sindri.Ui.Tests.Jobs;

/// <summary>The Jobs page and the job sheet in the shell: G J and the palette reach the page, a job link claims the sheet, and both read only while they are shown.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellJobsTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static DeepLink Link(string text) => DeepLink.TryParse(text, out var link) ? link : throw new InvalidOperationException(text);

    private async Task<ShellViewModel> Shell(bool connect)
    {
        if (connect)
        {
            _records.Write(A.Record());
        }

        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        if (connect)
        {
            _ = shell.Run(Token);
            await Until(() => shell.State == ShellState.Normal);
        }

        return shell;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition());
    }

    [Fact]
    public Task Deep_link_job_claims_the_sheet_from_the_placeholder_and_names_it_by_its_kind_once_read() => UiThread.Run(async () =>
    {
        _server.JobAnswers[Id] = RouteAnswer.Api("jobs-id", "running");
        var shell = await Shell(connect: true);

        shell.Navigate(Link("job/" + Id));
        await Until(() => shell.JobSheet.Phase == FramePhase.Shown);

        Assert.Equal((true, false, "Job Echo job"), (shell.IsJobSheet, shell.IsSheetPlaceholderShown, shell.SheetName));
        Assert.Equal("Reopen job Echo job", shell.EdgeTabName);
        Assert.False(shell.IsSessionSheet);
    }, Token);

    [Fact]
    public Task A_job_link_before_the_app_is_connected_names_the_job_by_its_id() => UiThread.Run(async () =>
    {
        var shell = await Shell(connect: false);

        shell.Navigate(Link("job/" + Id));

        Assert.Equal((true, FramePhase.Loading, "Job " + Id), (shell.IsJobSheet, shell.JobSheet.Phase, shell.SheetName));
    }, Token);

    [Fact]
    public Task Another_kind_does_not_name_a_job() => UiThread.Run(async () =>
    {
        var shell = await Shell(connect: false);
        shell.Navigate(Link("job/" + Id));

        shell.Navigate(Link("item/ITM-1"));

        Assert.False(shell.IsJobSheet);
    }, Token);

    [Fact]
    public Task G_J_goes_to_the_Jobs_page_which_then_reads() => UiThread.Run(async () =>
    {
        var shell = await Shell(connect: true);

        _ = shell.PressKey("G");
        _ = shell.PressKey("J");
        await Until(() => shell.Jobs.Drawn == 1);

        Assert.Equal((PageId.Jobs, true, 1), (shell.CurrentPage, shell.IsJobs, shell.Jobs.Rows.Count));
        Assert.Equal("Jobs", shell.PageName);
    }, Token);

    [Fact]
    public Task Jobs_is_a_page_but_not_a_tab() => UiThread.Run(async () =>
    {
        var shell = await Shell(connect: true);

        shell.GoTo(PageId.Jobs);

        Assert.Contains(PageId.Jobs, shell.Pages);
        Assert.DoesNotContain(shell.Navigation, n => n.Page == PageId.Jobs);
        Assert.Null(shell.SelectedNav);
    }, Token);

    [Fact]
    public Task The_page_reads_nothing_while_another_page_is_shown() => UiThread.Run(async () =>
    {
        var shell = await Shell(connect: true);
        await Task.Delay(50, Token);

        Assert.Equal(0, _server.JobsCalls);
    }, Token);

    [Fact]
    public Task A_row_opening_its_job_opens_the_sheet_over_the_page() => UiThread.Run(async () =>
    {
        _server.JobAnswers[Id] = RouteAnswer.Api("jobs-id", "running");
        var shell = await Shell(connect: true);
        shell.GoTo(PageId.Jobs);
        await Until(() => shell.Jobs.Rows.Count == 1);

        shell.Jobs.RequestOpen(shell.Jobs.Rows[0].Link);
        await Until(() => shell.JobSheet.Phase == FramePhase.Shown);

        Assert.Equal((PageId.Jobs, true, true), (shell.CurrentPage, shell.IsJobSheet, shell.IsSheetShown));
    }, Token);

    [Fact]
    public Task The_sheet_re_reads_only_while_it_is_open() => UiThread.Run(async () =>
    {
        _server.JobAnswers[Id] = RouteAnswer.Api("jobs-id", "running");
        var shell = await Shell(connect: true);
        shell.Navigate(Link("job/" + Id));
        await Until(() => shell.JobSheet.Phase == FramePhase.Shown);
        shell.CloseSheet();
        var before = _server.JobCalls;

        await shell.Health.RefreshViews(Url, Token);
        await Task.Delay(50, Token);

        Assert.Equal(before, _server.JobCalls);
    }, Token);

    [Fact]
    public Task Reopening_the_sheet_re_reads_a_job_that_ended_while_it_was_closed() => UiThread.Run(async () =>
    {
        _server.JobAnswers[Id] = RouteAnswer.Api("jobs-id", "running");
        var shell = await Shell(connect: true);
        shell.Navigate(Link("job/" + Id));
        await Until(() => shell.JobSheet.Phase == FramePhase.Shown && shell.JobSheet.Cancel is not null);
        shell.CloseSheet();
        _server.JobAnswers[Id] = RouteAnswer.Api("jobs-id", "result");

        shell.ReopenSheet();
        await Until(() => shell.JobSheet.Cancel is null);

        Assert.StartsWith("✓ Done", shell.JobSheet.StateLine, StringComparison.Ordinal);
    }, Token);
}
