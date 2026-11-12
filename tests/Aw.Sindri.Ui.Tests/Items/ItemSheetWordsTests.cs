using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Items;

/// <summary>The item sheet in product words (#3136): title first, plain empty states, plain history, rows named by the title, a first frame with the row's title.</summary>
[Lane(TestLane.Unit)]
public sealed class ItemSheetWordsTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<CatalogViewModel> Catalog()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        return catalog;
    }

    private async Task<ItemRowViewModel> Row()
    {
        var queue = new QueueViewModel(_server, await Catalog(), _clock);
        await queue.Refresh(_api, Token);
        return queue.Groups.SelectMany(g => g.Rows).First(r => r.Id == A.NewItem);
    }

    [Fact]
    public async Task The_sheet_header_is_the_title_and_never_the_raw_id()
    {
        var records = new InMemoryEndpointRecords();
        records.Write(A.Record());
        var shell = new ShellViewModel(records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        for (var i = 0; i < 400 && shell.Queue.Total == 0; i++)
        {
            await Task.Delay(5, Token);
        }

        var changed = new List<string>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");
        shell.OpenItem(A.NewItem);
        for (var i = 0; i < 400 && !shell.Item.HasItem; i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.Equal(("Try the queue", false), (shell.SheetTitle, shell.SheetTitleIsMono));
        Assert.Contains(nameof(ShellViewModel.SheetTitle), changed, StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_item_with_no_decisions_or_requests_says_so_without_its_id()
    {
        var sheet = new ItemSheetViewModel(_server, await Catalog(), _clock);

        await sheet.Open(_api, A.NewItem, Token);

        Assert.Equal(("No decisions yet.", "No requests yet."), (sheet.Needs.DecisionsEmpty, sheet.Needs.RequestsEmpty));
    }

    [Fact]
    public async Task History_reads_in_product_words()
    {
        var sheet = new ItemSheetViewModel(_server, await Catalog(), _clock);

        await sheet.Open(_api, A.NewItem, Token);

        Assert.Equal(("Created", "by you"), (sheet.Timeline[0].Type, sheet.Timeline[0].Actor));
        Assert.StartsWith("11:55:00 Created by you", sheet.Timeline[0].Name, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("item.state-changed", "State changed")]
    [InlineData("item.some-new-thing", "Some new thing")]
    public void An_event_type_reads_as_words_with_a_readable_fallback(string type, string words) =>
        Assert.Equal(words, Labels.TimelineType(type));

    [Theory]
    [InlineData("human:prover", "by you")]
    [InlineData("session:abc", "by an agent session")]
    [InlineData("service:host", "by Sindri")]
    public void An_actor_reads_as_words(string actor, string words) => Assert.Equal(words, Labels.TimelineActor(actor));

    [Fact]
    public async Task A_rows_open_button_and_name_lead_with_the_title()
    {
        var row = await Row();

        Assert.Equal(("Open Try the queue", "Try the queue, New"), (row.OpenName, row.Name));
    }

    [Fact]
    public async Task The_sheet_shows_the_rows_title_and_state_on_its_first_frame()
    {
        var sheet = new ItemSheetViewModel(_server, await Catalog(), _clock);

        sheet.Show(A.NewItem, await Row());

        Assert.Equal((true, false, "Try the queue", "New"), (sheet.HasItem, sheet.IsLoading, sheet.Title, sheet.StateWord));
    }
}
