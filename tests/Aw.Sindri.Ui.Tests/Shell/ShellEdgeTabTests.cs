using System.Text.RegularExpressions;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The edge tab of a closed sheet (issue 1089): the item's or workflow's readable title, the kind's plain word while it loads, never a raw address.</summary>
[Lane(TestLane.Unit)]
public sealed partial class ShellEdgeTabTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [GeneratedRegex(@"^ITM-|^desk:WFI-|/[0-9]+$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RawAddress { get; }

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);
        return shell;
    }

    [Fact]
    public Task An_item_sheets_edge_tab_reads_the_word_Item_then_the_items_title() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var gate = _server.ItemGates[A.NewItem] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        shell.Navigate(new DeepLink(DeepLinkKind.Item, A.NewItem));
        shell.CloseSheet();
        Assert.Equal(Labels.EdgeTabItem, shell.EdgeTabText);

        gate.SetResult();   // the held item read is served
        await ShellWait.Until(shell, () => shell.Item.Title == "Try the queue", Token);
        Assert.Equal("Try the queue", shell.EdgeTabText);
        Assert.DoesNotMatch(RawAddress, shell.EdgeTabText);
    }, Token);

    [Fact]
    public Task An_item_sheets_edge_tab_name_reads_Reopen_item_then_the_items_title_never_the_address() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var gate = _server.ItemGates[A.NewItem] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        shell.Navigate(new DeepLink(DeepLinkKind.Item, A.NewItem));
        shell.CloseSheet();
        Assert.Equal(Labels.Reopen + " item", shell.EdgeTabName);
        Assert.DoesNotContain(A.NewItem, shell.EdgeTabName, StringComparison.Ordinal);

        gate.SetResult();   // the held item read is served
        await ShellWait.Until(shell, () => shell.Item.Title == "Try the queue", Token);
        Assert.Contains("Try the queue", shell.EdgeTabName, StringComparison.Ordinal);
        Assert.DoesNotContain(A.NewItem, shell.EdgeTabName, StringComparison.Ordinal);
        Assert.DoesNotMatch(RawAddress, shell.EdgeTabName);
    }, Token);

    [Theory]
    [InlineData("job/JOB-9")]
    public Task Other_sheets_never_show_a_raw_address(string link) => UiThread.Run(async () =>
    {
        var shell = await Connected();
        Assert.True(DeepLink.TryParse(link, out var parsed));

        shell.Navigate(parsed);
        shell.CloseSheet();

        Assert.NotEmpty(shell.EdgeTabText);
        Assert.DoesNotMatch(RawAddress, shell.EdgeTabText);
    }, Token);

    // A needs-you link opens the inbox, not a sheet, so it has no edge tab; the row's own sheet is the one that does.
    [Fact]
    public Task A_needs_you_row_sheets_edge_tab_reads_Needs_you_never_the_address() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        Assert.True(DeepLink.TryParse("needs-you/ITM-01JC8", out var parsed));

        shell.NavigateToRowSheet(parsed);
        shell.CloseSheet();

        Assert.Equal(Labels.EdgeTabNeedsYou, shell.EdgeTabText);
        Assert.DoesNotMatch(RawAddress, shell.EdgeTabText);
        Assert.DoesNotMatch(RawAddress, shell.EdgeTabName);
    }, Token);
}
