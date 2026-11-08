using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The two focus regions on Home (todo spec D6, §3.3): a key acts only on the region that has focus, so X in the panel never rejects a decision and X in Needs you never checks a todo off.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoRegionKeysTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // A running shell with one decision selected in Needs you.
    private async Task<ShellViewModel> WithDecision()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Decision()], ""));
        _ = shell.Press("J");
        return shell;
    }

    [Fact]
    public Task X_with_focus_in_the_panel_never_calls_Needs_yous_reject() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();
        shell.Todo.HasFocus = true;

        Assert.True(shell.Press("X"));   // the key is spent, nothing else sees it
        await shell.Decision.Settled;

        Assert.Empty(_server.DecideCalls);
    }, Token);

    [Fact]
    public Task A_Y_and_Enter_with_focus_in_the_panel_never_reach_Needs_you_either() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();
        shell.Todo.HasFocus = true;

        Assert.True(shell.Press("A"));
        Assert.True(shell.Press("Y"));
        Assert.True(shell.Press("Enter"));
        await shell.Decision.Settled;

        Assert.Empty(_server.DecideCalls);
        Assert.False(shell.IsSheetShown);
    }, Token);

    [Fact]
    public Task X_with_focus_in_Needs_you_rejects_the_decision_and_checks_no_todo_off() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();
        shell.Todo.HasFocus = false;

        Assert.True(shell.Press("X"));
        await shell.Decision.Settled;

        Assert.Equal([("DEC-1", "decline")], [.. _server.DecideCalls.Select(c => (c.Decision, c.Request.Choice))]);
        Assert.Empty(_server.TodoWrites);
    }, Token);
}
