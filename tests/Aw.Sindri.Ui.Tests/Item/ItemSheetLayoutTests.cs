using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>The item sheet's layout (F3 spec work §2, slice 5): an open decision is a callout that points to Needs you, and an ended item offers no actions.</summary>
[Lane(TestLane.Unit)]
public sealed class ItemSheetLayoutTests
{
    private const string DecisionItem = "ITM-0000000000000000000000000000000e";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ItemSheetViewModel> Sheet(string id)
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var sheet = new ItemSheetViewModel(_server, catalog, _clock);
        await sheet.Open(_api, id, Token);
        return sheet;
    }

    [Fact]
    public async Task An_open_decision_shows_the_callout_and_a_link_to_needs_you()
    {
        _server.DecisionsByItem[DecisionItem] = RouteAnswer.Api("decisions", "by-item");
        _server.Items[DecisionItem] = _server.Items[A.NewItem];

        var sheet = await Sheet(DecisionItem);

        Assert.True(sheet.HasOpenDecision);
        Assert.Equal(DecisionItem, sheet.NeedsYouItem);
    }

    [Fact]
    public async Task An_item_with_no_decision_shows_no_callout()
    {
        var sheet = await Sheet(A.NewItem);

        Assert.False(sheet.HasOpenDecision);
    }

    [Fact]
    public async Task An_ended_item_offers_no_actions()
    {
        _server.Items[DecisionItem] = new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("items-id", "after-close"));

        var sheet = await Sheet(DecisionItem);

        Assert.False(sheet.HasActions);
    }
}
