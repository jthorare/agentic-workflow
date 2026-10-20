using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>One selection on Home (review run 1, K1): a queue row chosen after a Needs-you or strip row owns the keys, and the open-sheet marker never outlives its sheet.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellSelectionTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting"), NeedsYouView = RouteAnswer.Api("needs-you", "empty"), WorkflowAnswer = RouteAnswer.Api("workflow-instances", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeClipboard _clipboard = new();

    private const string QueueRow = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a301";

    // The golden queue with one row's primary action set to Decide, so Y has a command to copy there.
    private static RouteAnswer WithCopyableRow()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.SpecShaped("queue", "extra-p4-waiting"))!;
        foreach (var item in root["groups"]!.AsArray().SelectMany(g => g!["items"]!.AsArray()).Where(i => string.Equals(i!["id"]!.GetValue<string>(), QueueRow, StringComparison.Ordinal)))
        {
            item!["primaryAction"] = "Decide";
        }

        return new RouteAnswer(System.Net.HttpStatusCode.OK, root.ToJsonString());
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        _server.QueueView = WithCopyableRow();
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), _clipboard);
        await shell.Restore(Token);
        _ = shell.Run(Token);
        for (var i = 0; i < 400 && !(shell.State == ShellState.Normal && shell.Queue.RunningRows.Count + shell.Queue.QueuedRows.Count > 0); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.Equal(ShellState.Normal, shell.State);
        return shell;
    }

    [Fact]
    public Task A_queue_row_chosen_after_an_open_decision_owns_A_Y_and_Enter() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Decision()], ""));
        Assert.True(shell.Press("J"));
        Assert.True(shell.Press("Enter"));
        Assert.True(shell.IsDecisionSheet);
        const string queueRow = QueueRow;

        shell.SelectQueueRow(queueRow);
        const string expected = "sindri decide <decisionId> <choice> [--note <note>]";
        shell.OpenItem(queueRow);

        Assert.Null(shell.NeedsYou.SelectedId);
        Assert.False(shell.Press("A"));
        await shell.Decision.Settled;
        Assert.Empty(_server.DecideCalls);
        Assert.True(shell.Press("Y"));
        await Task.Delay(1, Token);
        Assert.Equal(expected, _clipboard.Text);
        Assert.True(shell.Press("Enter"));
        Assert.Equal(DeepLinkKind.Item, shell.SheetLink?.Kind);
    }, Token);

    [Fact]
    public Task A_queue_row_chosen_after_a_strip_row_owns_the_keys() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.Strip.Update(FixtureInstances.Root);
        shell.SelectStripRow(shell.Strip.Rows[0].Address);
        const string queueRow = QueueRow;

        shell.SelectQueueRow(queueRow);
        const string expected = "sindri decide <decisionId> <choice> [--note <note>]";

        Assert.Null(shell.Strip.SelectedAddress);
        Assert.True(shell.Press("Y"));
        await Task.Delay(1, Token);
        Assert.Equal(expected, _clipboard.Text);
    }, Token);

    [Fact]
    public Task Another_sheet_replacing_the_decision_sheet_clears_the_open_marker() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Decision()], ""));
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));
        Assert.Equal("decision:DEC-1", shell.NeedsYou.SheetRowId);

        shell.OpenSheet(new DeepLink(DeepLinkKind.Item, "ITM-01JC8"));

        Assert.Null(shell.NeedsYou.SheetRowId);
    }, Token);

    [Fact]
    public Task Escape_within_the_key_quiet_still_closes_the_sheet_and_returns_focus_to_the_row() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Decision(), NeedsYouRows.Failure()], ""));
        shell.NavigateToRowSheet(new DeepLink(DeepLinkKind.NeedsYou, "failure:service:index"));
        var focused = new List<string>();
        shell.NeedsYou.FocusRequested += (_, e) => focused.Add(e.RowId);
        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Decision()], ""));   // the selected row cleared: an automatic move

        shell.CloseSheet();

        Assert.Null(shell.NeedsYou.SheetRowId);
        Assert.False(shell.IsSheetOpen);
        Assert.NotNull(shell.NeedsYou.SelectedId);
        Assert.Equal(shell.NeedsYou.SelectedId, Assert.Single(focused));
    }, Token);
}
