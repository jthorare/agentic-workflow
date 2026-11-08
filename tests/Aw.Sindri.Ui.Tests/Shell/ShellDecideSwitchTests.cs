using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>A decision switch while Decide is in flight sticks to the decision that was pressed (review run 1, K6 / corr-4).</summary>
[Lane(TestLane.Unit)]
public sealed class ShellDecideSwitchTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NeedsYouEntry Second() => NeedsYouRows.Decision("decision:DEC-2") with { Subject = new NeedsYouSubject("decision", "DEC-2") };

    [Fact]
    public Task Showing_D2_while_D1_is_in_flight_leaves_D2_undecided_and_pressable() => UiThread.Run(async () =>
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        for (var i = 0; i < 400 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(5, Token);
        }

        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Decision(), Second()], ""));
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));
        _server.CommandGate = new TaskCompletionSource();
        var first = shell.Decision.Choose("accept");

        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-2"));
        _server.CommandGate.SetResult();
        await first;

        Assert.Equal("DEC-2", shell.Decision.DecisionId);
        Assert.Empty(shell.Decision.Outcome);
        Assert.All(shell.Decision.Options, o => Assert.True(o.IsEnabled));
        _server.CommandGate = null;
        await shell.Decision.Choose("decline");
        Assert.Equal([("DEC-1", "accept"), ("DEC-2", "decline")], [.. _server.DecideCalls.Select(c => (c.Decision, c.Request.Choice))]);
    }, Token);
}
