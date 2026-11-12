using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Ui.Tests.Agents.Page.AgentsPageFixture;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>#3108: the Agents rows re-word "for 10s" on the shell's one-second tick, not only on the 5 s poll.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionRowAgoTickTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(AsOf);

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
    public Task The_for_column_ticks_between_polls() => UiThread.Run(async () =>
    {
        var list = Edited(l =>
        {
            foreach (var row in l["sessions"]!.AsArray())
            {
                row!["since"] = (AsOf - TimeSpan.FromSeconds(10)).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            }
        });
        _server.SessionAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());
        var shell = await Shell();
        shell.GoTo(PageId.Agents);
        await Until(() => shell.Agents.Groups.SelectMany(g => g.Rows).Any());
        var row = shell.Agents.Groups.SelectMany(g => g.Rows).First();
        var before = row.SinceText;

        _clock.Advance(TimeSpan.FromSeconds(2));   // under the 5 s poll: only the shell's beat can re-word it

        await Until(() => !string.Equals(row.SinceText, before, StringComparison.Ordinal));
    }, Token);
}
