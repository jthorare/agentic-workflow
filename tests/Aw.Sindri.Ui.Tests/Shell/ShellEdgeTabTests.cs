using System.Text.RegularExpressions;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
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
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Workflows.Groups.Count > 0, Token);
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
    public Task A_workflow_sheets_edge_tab_reads_the_kind_word_before_its_title_loads_and_the_title_after() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var detail = FixtureInstances.Detail("project-build");
        var gate = _server.InstanceGates[detail.Id] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        shell.OpenInstance(detail.Address, id: null);
        shell.CloseSheet();
        Assert.Equal(Labels.EdgeTabWorkflowRun, shell.EdgeTabText);
        Assert.DoesNotMatch(RawAddress, shell.EdgeTabText);

        gate.SetResult();   // the held instance read is served (the list already named its id)
        await ShellWait.Until(shell, () => shell.Instance.Phase == FramePhase.Shown && shell.Instance.Address == detail.Address, Token);
        Assert.Equal(detail.Title, shell.EdgeTabText);
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

    [Fact]
    public Task A_workflow_sheets_edge_tab_name_reads_Reopen_workflow_then_the_runs_title_never_the_address() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        var detail = FixtureInstances.Detail("project-build");
        var gate = _server.InstanceGates[detail.Id] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        shell.OpenInstance(detail.Address, id: null);
        shell.CloseSheet();
        Assert.Equal(Labels.Reopen + " workflow", shell.EdgeTabName);
        Assert.DoesNotContain(detail.Address, shell.EdgeTabName, StringComparison.Ordinal);

        gate.SetResult();   // the held instance read is served (the list already named its id)
        await ShellWait.Until(shell, () => shell.Instance.Phase == FramePhase.Shown && shell.Instance.Address == detail.Address, Token);
        Assert.Contains(detail.Title, shell.EdgeTabName, StringComparison.Ordinal);
        Assert.DoesNotContain(detail.Address, shell.EdgeTabName, StringComparison.Ordinal);
        Assert.DoesNotMatch(RawAddress, shell.EdgeTabName);
    }, Token);

    [Fact]
    public Task A_workflow_type_sheets_edge_tab_reads_the_workflows_name_never_the_recipe_id() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        Assert.True(DeepLink.TryParse("recipe/workflow.scoping", out var parsed));

        shell.Navigate(parsed);
        shell.CloseSheet();

        Assert.NotEmpty(shell.Recipe.Title);   // the sheet's title, which is never the id
        Assert.Equal(shell.Recipe.Title, shell.EdgeTabText);
        Assert.DoesNotContain("workflow.scoping", shell.EdgeTabText, StringComparison.Ordinal);
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
