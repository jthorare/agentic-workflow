using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Connections;

/// <summary>#3108: the Connections page rows re-word "Refreshed 30m ago" on the shell's one-second tick.</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionAgoTickTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

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
    public Task Refreshed_ago_on_a_connection_row_ticks() => UiThread.Run(async () =>
    {
        _ = _server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work") with { LastRefreshedAt = _clock.GetUtcNow() - TimeSpan.FromMinutes(30) });
        var shell = await Shell();
        shell.GoTo(PageId.Connections);
        await Until(() => shell.Connections.Rows.Count == 1);
        var before = shell.Connections.Rows[0].RefreshedText;
        Assert.Equal("Refreshed 30m ago", before);

        _clock.Advance(TimeSpan.FromMinutes(2));

        await Until(() => !string.Equals(shell.Connections.Rows[0].RefreshedText, before, StringComparison.Ordinal));
    }, Token);
}
