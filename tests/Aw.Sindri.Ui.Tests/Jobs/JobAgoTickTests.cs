using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Ui.Tests.Jobs.JobsFixture;

namespace Aw.Sindri.Ui.Tests.Jobs;

/// <summary>#3108: the Jobs page rows and the job sheet re-word "Started 5m ago" on the shell's one-second tick.</summary>
[Lane(TestLane.Unit)]
public sealed class JobAgoTickTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(Now);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Shell()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.State == ShellState.Normal);
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
    public Task Started_ago_on_a_job_row_ticks() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.GoTo(PageId.Jobs);
        await Until(() => shell.Jobs.Rows.Count > 0);
        var before = shell.Jobs.Rows[0].WhenText;

        _clock.Advance(TimeSpan.FromMinutes(2));

        await Until(() => !string.Equals(shell.Jobs.Rows[0].WhenText, before, StringComparison.Ordinal));
    }, Token);

    [Fact]
    public Task Started_ago_on_the_job_sheet_ticks() => UiThread.Run(async () =>
    {
        _server.JobAnswers[Id] = RouteAnswer.Api("jobs-id", "running");
        var shell = await Shell();
        shell.Navigate(DeepLink.TryParse("job/" + Id, out var link) ? link : throw new InvalidOperationException(Id));
        await Until(() => shell.JobSheet.Phase == FramePhase.Shown);
        var before = shell.JobSheet.StateLine;

        _clock.Advance(TimeSpan.FromMinutes(2));

        await Until(() => !string.Equals(shell.JobSheet.StateLine, before, StringComparison.Ordinal));
    }, Token);
}
