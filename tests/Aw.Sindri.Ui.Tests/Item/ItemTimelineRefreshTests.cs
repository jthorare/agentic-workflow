using System.Globalization;
using System.Net;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>
/// The item sheet's timeline on an event (#3800): one read after the newest line shown, appended in order, never a re-read of
/// every loaded page; the pages are read again from the first only on another connection or after the views rebuilt.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ItemTimelineRefreshTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The page the fake serves after 'after': lines first..last (sequence N says "Event N", or "Reworded N" when reworded).
    private void Serve(long after, long first, long last, long? next, bool rebuilding = false, bool reworded = false)
    {
        var json = new StringBuilder("{\"entries\":[");
        for (var i = first; i <= last; i++)
        {
            var line = (reworded ? "Reworded " : "Event ") + i.ToString(CultureInfo.InvariantCulture);
            _ = json.Append(CultureInfo.InvariantCulture, $"{(i > first ? "," : string.Empty)}{{\"sequence\":{i},\"time\":\"2026-10-11T11:55:00.0000000Z\",\"type\":\"item.note\",\"actor\":\"human:cli\",\"line\":\"{line}\"}}");
        }

        var cursor = next is { } n ? n.ToString(CultureInfo.InvariantCulture) : "null";
        var rebuild = rebuilding ? "true" : "false";
        _ = json.Append(CultureInfo.InvariantCulture, $"],\"next\":{cursor},\"asOf\":{{\"cursor\":57,\"ledgerHead\":57,\"projectedAt\":null,\"rebuilding\":{rebuild},\"stalled\":false}}}}");
        _server.ItemTimelinePages[(A.NewItem, after)] = new RouteAnswer(HttpStatusCode.OK, json.ToString());
    }

    private async Task<ItemSheetViewModel> Sheet()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        return new ItemSheetViewModel(_server, catalog, _clock);
    }

    // Three pages loaded, the last one the end: lines 1..250.
    private async Task<ItemSheetViewModel> ThreePagesLoaded()
    {
        Serve(0, 1, 100, 100);
        Serve(100, 101, 200, 200);
        Serve(200, 201, 250, next: null);
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        Assert.True(await sheet.LoadMore(Token));
        Assert.True(await sheet.LoadMore(Token));
        Assert.Equal((250, false), (sheet.Timeline.Count, sheet.HasMore));
        return sheet;
    }

    private static void InOrderOnce(ItemSheetViewModel sheet, long last)
    {
        var sequences = sheet.Timeline.Select(r => r.Sequence).ToList();
        Assert.Equal(Enumerable.Range(1, (int)last).Select(i => (long)i), sequences);
    }

    [Fact]
    public async Task An_event_on_a_sheet_with_three_loaded_pages_makes_one_timeline_read()
    {
        var sheet = await ThreePagesLoaded();
        Serve(250, 251, 253, next: null);
        var first = sheet.Timeline[0];
        var calls = _server.TimelineCalls;

        await sheet.Refresh(_api, Token);

        Assert.Equal(calls + 1, _server.TimelineCalls);
        InOrderOnce(sheet, 253);
        Assert.Equal(("Event 251", false), (sheet.Timeline[250].Line, sheet.HasMore));
        Assert.Same(first, sheet.Timeline[0]);   // the rows shown are untouched: their controls, focus and scroll stay
    }

    [Fact]
    public async Task Lines_the_sheet_already_shows_are_never_added_twice()
    {
        var sheet = await ThreePagesLoaded();
        Serve(250, 249, 252, next: null);   // overlaps the lines shown

        await sheet.Refresh(_api, Token);
        await sheet.Refresh(_api, Token);   // after 252 the fake answers an empty last page

        InOrderOnce(sheet, 252);
    }

    // The newest lines sit past the pages Load more hasn't read yet: nothing the sheet shows can change, so it reads nothing.
    [Fact]
    public async Task An_event_while_more_pages_wait_reads_no_timeline_page()
    {
        Serve(0, 1, 100, 100);
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var calls = _server.TimelineCalls;

        await sheet.Refresh(_api, Token);

        Assert.Equal(calls, _server.TimelineCalls);
        Assert.Equal((100, true), (sheet.Timeline.Count, sheet.HasMore));
    }

    [Fact]
    public async Task Load_more_after_an_event_added_a_page_of_lines_pages_on_from_there()
    {
        Serve(0, 1, 50, next: null);
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        Serve(50, 51, 150, 150);   // more than a page arrived

        await sheet.Refresh(_api, Token);
        Assert.Equal((150, true), (sheet.Timeline.Count, sheet.HasMore));

        Serve(150, 151, 160, next: null);
        Assert.True(await sheet.LoadMore(Token));

        InOrderOnce(sheet, 160);
        Assert.False(sheet.HasMore);
    }

    // A rebuild replays the ledger into the views: the lines shown may be gone or reworded, so the next read takes every page
    // shown again from the first, and the read after that is one read after the newest line again.
    [Fact]
    public async Task After_the_views_said_they_were_rebuilding_the_next_read_rereads_the_shown_pages()
    {
        var sheet = await ThreePagesLoaded();
        Serve(250, 0, -1, next: null, rebuilding: true);
        await sheet.Refresh(_api, Token);
        Serve(0, 1, 100, 100, reworded: true);
        Serve(100, 101, 200, 200, reworded: true);
        Serve(200, 201, 250, next: null, reworded: true);
        var calls = _server.TimelineCalls;

        await sheet.Refresh(_api, Token);

        Assert.Equal(calls + 3, _server.TimelineCalls);
        InOrderOnce(sheet, 250);
        Assert.Equal(("Reworded 1", "Reworded 250"), (sheet.Timeline[0].Line, sheet.Timeline[^1].Line));

        Serve(250, 251, 251, next: null);
        await sheet.Refresh(_api, Token);
        Assert.Equal(calls + 4, _server.TimelineCalls);
        InOrderOnce(sheet, 251);
    }

    [Fact]
    public async Task A_refresh_on_another_connection_rereads_the_shown_pages_from_the_first()
    {
        var sheet = await ThreePagesLoaded();
        var calls = _server.TimelineCalls;

        await sheet.Refresh(new Uri("http://127.0.0.1:7411/"), Token);

        Assert.Equal(calls + 3, _server.TimelineCalls);
        InOrderOnce(sheet, 250);
    }
}
