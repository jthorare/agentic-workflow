using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>A decision refused on the item sheet can be pressed again (review run 1, K6 / corr-7): "never retried" is the app's rule, not the human's.</summary>
[Lane(TestLane.Unit)]
public sealed class ItemDecideRetryTests
{
    private const string DecisionItem = "ITM-0000000000000000000000000000000e";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task A_decide_the_views_could_not_take_yet_can_be_pressed_again() => UiThread.Run(async () =>
    {
        _server.DecisionsByItem[DecisionItem] = RouteAnswer.Api("decisions", "by-item");
        _server.RequestsByItem[DecisionItem] = RouteAnswer.Api("requests", "list-by-item");
        _server.Items[DecisionItem] = _server.Items[A.NewItem];
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var sheet = new ItemSheetViewModel(_server, catalog, _clock);
        await sheet.Open(_api, DecisionItem, Token);
        var decision = sheet.Needs.Decisions[0];
        _server.Decided = RouteAnswer.Problem("decisions-id", "while-stalled");

        await sheet.Needs.Choose(decision.Id, "accept");

        Assert.All(sheet.Needs.Decisions[0].Options, o => Assert.True(o.IsEnabled));
        _server.Decided = RouteAnswer.Api("decisions-id", "accepted");
        await sheet.Needs.Choose(decision.Id, "accept");
        Assert.Equal(2, _server.DecideCalls.Count);
        Assert.Equal(string.Empty, sheet.Needs.Refusal);
    }, Token);

    [Fact]
    public Task A_re_read_of_equal_decisions_keeps_the_same_rows_and_buttons() => UiThread.Run(async () =>
    {
        _server.DecisionsByItem[DecisionItem] = RouteAnswer.Api("decisions", "by-item");
        _server.RequestsByItem[DecisionItem] = RouteAnswer.Api("requests", "list-by-item");
        _server.Items[DecisionItem] = _server.Items[A.NewItem];
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var sheet = new ItemSheetViewModel(_server, catalog, _clock);
        await sheet.Open(_api, DecisionItem, Token);
        var before = sheet.Needs.Decisions;
        var changes = 0;
        sheet.Needs.PropertyChanged += (_, e) => changes += string.Equals(e.PropertyName, nameof(ItemNeedsViewModel.Decisions), StringComparison.Ordinal) ? 1 : 0;

        await sheet.Needs.Read(_api, DecisionItem, Token);

        Assert.Same(before, sheet.Needs.Decisions);
        Assert.Equal(0, changes);
    }, Token);

    // Review run 2 (L3 / corr-2): the sheet shows only its own item's needs.
    private async Task<ItemSheetViewModel> SheetRefusedOnDecisionItem()
    {
        _server.DecisionsByItem[DecisionItem] = RouteAnswer.Api("decisions", "by-item");
        _server.RequestsByItem[DecisionItem] = RouteAnswer.Api("requests", "list-by-item");
        _server.Items[DecisionItem] = _server.Items[A.NewItem];
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var sheet = new ItemSheetViewModel(_server, catalog, _clock);
        await sheet.Open(_api, DecisionItem, Token);
        _server.Decided = RouteAnswer.Problem("decisions-id", "while-stalled");
        await sheet.Needs.Choose(sheet.Needs.Decisions[0].Id, "accept");
        Assert.NotEmpty(sheet.Needs.Refusal);
        return sheet;
    }

    [Fact]
    public Task Opening_another_item_drops_the_first_ones_decisions_and_refusal_at_once() => UiThread.Run(async () =>
    {
        var sheet = await SheetRefusedOnDecisionItem();

        sheet.Show(A.NewItem);

        Assert.Empty(sheet.Needs.Decisions);
        Assert.Empty(sheet.Needs.Requests);
        Assert.Equal(string.Empty, sheet.Needs.Refusal);
        Assert.Equal(string.Empty, sheet.Needs.DecisionsEmpty);
    }, Token);

    [Fact]
    public Task Another_items_failed_read_does_not_leave_the_first_ones_decisions() => UiThread.Run(async () =>
    {
        var sheet = await SheetRefusedOnDecisionItem();
        _server.DecisionsByItem[A.NewItem] = RouteAnswer.Problem("decisions-id", "while-stalled");

        await sheet.Open(_api, A.NewItem, Token);

        Assert.Empty(sheet.Needs.Decisions);
        Assert.Equal(string.Empty, sheet.Needs.Refusal);
    }, Token);

    [Fact]
    public Task Choose_sets_a_refusal_only_if_the_sheet_is_still_on_the_pressed_item() => UiThread.Run(async () =>
    {
        var sheet = await SheetRefusedOnDecisionItem();
        sheet.Show(A.NewItem);
        await sheet.Open(_api, DecisionItem, Token);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _server.CommandGate = gate;
        _server.Decided = RouteAnswer.Problem("decisions-id", "while-stalled");

        var pressed = sheet.Needs.Choose(sheet.Needs.Decisions[0].Id, "accept");
        sheet.Show(A.NewItem);
        gate.SetResult();
        await pressed;

        Assert.Equal(string.Empty, sheet.Needs.Refusal);
        Assert.Empty(sheet.Needs.Decisions);
    }, Token);
}
