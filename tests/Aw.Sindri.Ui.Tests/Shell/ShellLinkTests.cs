using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>One link rule (review run 1, K14 / arch-0, corr-12): a needs-you link whose row is a decision opens the decision sheet, from the tray as from a click.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellLinkTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task A_tray_row_for_a_decision_opens_the_decision_sheet_with_its_options() => UiThread.Run(async () =>
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        for (var i = 0; i < 400 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(5, Token);
        }

        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Park(), NeedsYouRows.Decision()], ""));

        shell.Navigate(new DeepLink(DeepLinkKind.NeedsYou, "decision:DEC-1"));

        Assert.True(shell.IsDecisionSheet);
        Assert.Equal(["accept", "decline"], shell.Decision.Options.Select(o => o.Id), StringComparer.Ordinal);
        Assert.Equal("decision:DEC-1", shell.NeedsYou.SelectedId);
    }, Token);
}
