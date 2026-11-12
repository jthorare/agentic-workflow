using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3108: the Workflows list rows re-word "Changed by you, 5m ago" on the shell's one-second tick.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowRowAgoTickTests
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
    public Task Changed_ago_on_a_workflow_row_ticks() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.GoTo(PageId.Workflows);
        await Until(() => shell.Workflows.Rows.Count == 2);
        var before = shell.Workflows.Rows[0].ChangedText;

        _clock.Advance(TimeSpan.FromMinutes(2));

        await Until(() => !string.Equals(shell.Workflows.Rows[0].ChangedText, before, StringComparison.Ordinal));
    }, Token);
}
