using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The shell hosts the Needs-you list on Home (U3' T9): the filter G D sets, the deep links, J / K across the panels, Mark all seen.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellNeedsYouTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting"), NeedsYouView = RouteAnswer.Api("needs-you", "empty"), WorkflowAnswer = RouteAnswer.Api("workflow-instances", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NeedsYouFeed Feed(params NeedsYouEntry[] rows) => new(rows, "") { Counts = new HomeCounts(Running: 2, WithYou: 0, Queued: 1) };

    private static string[] Ids(ShellViewModel shell) => [.. shell.NeedsYou.Rows.Select(r => r.Id)];

    private async Task<ShellViewModel> Connected()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && shell.Queue.Total > 0 && shell.Queue.RunningRows.Count + shell.Queue.QueuedRows.Count > 0, Token);

        Assert.Equal(ShellState.Normal, shell.State);
        return shell;
    }

    [Fact]
    public Task The_shell_hosts_the_list_the_read_fills() => UiThread.Run(async () =>
    {
        var shell = await Connected();

        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));

        Assert.Equal(["park:ITM-01JC8", "failure:service:index"], Ids(shell));
        Assert.Equal(2, shell.NeedsYou.Count);
    }, Token);

    [Fact]
    public Task G_D_filters_Home_to_the_decision_rows_and_any_other_chord_clears_it() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Decision(), NeedsYouRows.Failure()));

        shell.GoTo(PageId.Home, HomeFilter.Decisions);
        Assert.Equal(["decision:DEC-1"], Ids(shell));

        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Decision(), NeedsYouRows.Failure(), NeedsYouRows.Decision("decision:DEC-2")));
        Assert.Equal(["decision:DEC-1", "decision:DEC-2"], Ids(shell));

        shell.GoTo(PageId.Home);
        Assert.Equal(4, shell.NeedsYou.Count);
    }, Token);

    [Fact]
    public Task The_filter_does_not_change_the_badge() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Decision()));

        shell.GoTo(PageId.Home, HomeFilter.Decisions);

        Assert.Equal(2, shell.Attention.Badge);
    }, Token);

    [Fact]
    public Task Deep_link_needs_you_rowId_selects_the_row_and_opens_the_inbox_on_it() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));

        shell.Navigate(new DeepLink(DeepLinkKind.NeedsYou, "failure:service:index"));

        Assert.Equal(("failure:service:index", true, false), (shell.NeedsYou.SelectedId, shell.IsNeedsYouInbox, shell.IsSheetOpen));
    }, Token);

    [Fact]
    public Task The_row_sheet_link_selects_the_row_and_opens_its_sheet_over_the_page() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));

        shell.NavigateToRowSheet(new DeepLink(DeepLinkKind.NeedsYou, "failure:service:index"));

        Assert.Equal(("failure:service:index", "failure:service:index", true, false, false), (shell.NeedsYou.SelectedId, shell.NeedsYou.SheetRowId, shell.IsSheetOpen, shell.IsSheetPlaceholderShown, shell.IsNeedsYouInbox));
        Assert.True(shell.IsNeedsYouSheet);
    }, Token);

    [Fact]
    public Task Deep_link_decision_id_selects_its_row_and_opens_the_decision_sheet() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Decision()));

        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));

        Assert.Equal(("decision:DEC-1", true, false), (shell.NeedsYou.SelectedId, shell.IsSheetOpen, shell.IsSheetPlaceholderShown));
        Assert.True(shell.IsDecisionSheet);
    }, Token);

    [Fact]
    public Task J_and_K_walk_Needs_you_then_Running_then_Queued_and_back() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Failure()));
        var running = shell.Queue.RunningRows.Concat(shell.Queue.QueuedRows).First().Id;

        Assert.True(shell.Press("J"));
        Assert.Equal("park:ITM-01JC8", shell.NeedsYou.SelectedId);
        _clock.Advance(NeedsYouViewModel.KeyQuiet);
        Assert.True(shell.Press("J"));
        Assert.Equal("failure:service:index", shell.NeedsYou.SelectedId);
        Assert.True(shell.Press("J"));
        Assert.Equal((null, running), (shell.NeedsYou.SelectedId, shell.Queue.SelectedId));

        Assert.True(shell.Press("K"));
        Assert.Equal(("failure:service:index", false), (shell.NeedsYou.SelectedId, shell.Queue.HasSelectionOn(QueueScope.Home)));
    }, Token);

    [Fact]
    public Task Mark_all_seen_marks_the_current_rows_and_none_that_arrive_later() => UiThread.Run(async () =>
    {
        var shell = await Connected();
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park()));
        Assert.Equal(RowTint.Pulsing, shell.NeedsYou.Rows[0].Tint);

        shell.MarkAllSeen();
        shell.ShowNeedsYou(Feed(NeedsYouRows.Park(), NeedsYouRows.Park("park:ITM-NEW")));

        Assert.Equal([RowTint.None, RowTint.Pulsing], [.. shell.NeedsYou.Rows.Select(r => r.Tint)]);
    }, Token);

    [Fact]
    public void A_row_chip_is_the_served_state_chip_the_one_chip_control_draws()
    {
        var park = new NeedsYouRowViewModel(NeedsYouRows.Park(), seen: false, reducedMotion: false, arrived: false, new FakeTimeProvider(A.Noon));

        Assert.Equal(new StateChip("PARKED", ChipTone.Attn), park.Chip);
    }
}
