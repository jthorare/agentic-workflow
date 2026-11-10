using System.Globalization;
using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.Views.Layout;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>The Work page's Sindri table when it has little to show (issue 2551): the one empty line, headers only over rows, Closed only with closed items, a footer only when behind, and no column that is empty on every row.</summary>
[Lane(TestLane.Unit)]
public sealed class QueueWorkTableTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ItemSummaryView Item(string id, string? repo) =>
        new(id, "Title " + id, repo, "new", Step: null, A.Noon.AddMinutes(-5), PrimaryAction: null, Actions: []);

    private async Task<QueueViewModel> Queue(IReadOnlyList<ItemSummaryView> queued, AsOfView? asOf = null, int closed = 0)
    {
        var view = new QueueView(
            [new QueueGroupView("queued", queued, "Nothing is queued.")],
            "Nothing in the queue. Add one with `sindri item add`.",
            asOf ?? new AsOfView(214, 214, A.Noon, Rebuilding: false, Stalled: false));
        _server.QueueView = new RouteAnswer(HttpStatusCode.OK, JsonSerializer.Serialize(view, SindriApiClient.Json));
        _server.ListItemsBody = state =>
        {
            var items = string.Equals(state, "done", StringComparison.Ordinal)
                ? Enumerable.Range(1, closed).Select(i => new ItemSummaryView(string.Create(CultureInfo.InvariantCulture, $"ITM-C{i}"), string.Create(CultureInfo.InvariantCulture, $"Closed {i}"), Repo: null, State: "done", Step: null, Since: A.Noon.AddHours(-i), PrimaryAction: null, Actions: [])).ToArray()
                : [];
            var list = new ItemListView(items, Empty: null, new AsOfView(1, 1, A.Noon, Rebuilding: false, Stalled: false));
            return new RouteAnswer(HttpStatusCode.OK, JsonSerializer.Serialize(list, SindriApiClient.Json));
        };
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var queue = new QueueViewModel(_server, catalog, _clock);
        await queue.Refresh(_api, Token);
        return queue;
    }

    [Fact]
    public async Task An_empty_queue_says_one_line_and_offers_a_create_item_link()
    {
        var queue = await Queue([]);

        Assert.True(queue.ShowsNothing);
        Assert.Equal("Nothing in the queue.", queue.NothingText);
        Assert.Equal("Create item", queue.CreateLinkText);
    }

    [Fact]
    public async Task The_column_headers_show_only_over_rows()
    {
        Assert.False((await Queue([])).ShowsColumns);
        Assert.True((await Queue([Item("ITM-1", repo: "alpha")])).ShowsColumns);
    }

    [Fact]
    public async Task The_closed_fold_is_hidden_with_no_closed_items_and_shows_its_count_with_some()
    {
        var none = await Queue([Item("ITM-1", repo: "alpha")], closed: 0);
        var some = await Queue([Item("ITM-1", repo: "alpha")], closed: 3);

        Assert.False(none.ShowsClosed);
        Assert.True(some.ShowsClosed);
        Assert.Equal(("Closed 3", "3"), (some.ClosedHeader, some.ClosedCount));
    }

    [Fact]
    public async Task The_footer_is_empty_when_the_views_are_current()
    {
        var queue = await Queue([Item("ITM-1", repo: "alpha")]);

        Assert.Equal(string.Empty, queue.FooterText);
        Assert.False(queue.HasFooter);
    }

    [Fact]
    public async Task The_footer_says_when_it_was_updated_and_never_an_event_number_when_the_views_are_behind()
    {
        var queue = await Queue([Item("ITM-1", repo: "alpha")], new AsOfView(336, 337, A.Noon.AddMinutes(-5), Rebuilding: false, Stalled: false));

        Assert.True(queue.HasFooter);
        Assert.Equal("Updated 11:55, 5 min ago", queue.FooterText);
        Assert.DoesNotContain("336", queue.FooterText, StringComparison.Ordinal);
        Assert.DoesNotContain("event", queue.FooterText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_repo_column_is_dropped_when_every_row_has_no_repo_and_kept_when_one_has()
    {
        var empty = await Queue([Item("ITM-1", repo: null), Item("ITM-2", repo: null)]);
        var one = await Queue([Item("ITM-1", repo: null), Item("ITM-2", repo: "alpha")]);

        Assert.False(empty.ShowsRepoColumn);
        Assert.True(one.ShowsRepoColumn);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(700)]
    [InlineData(950)]
    [InlineData(1400)]
    public void Age_is_kept_at_every_width_with_or_without_a_repo_column(int width)
    {
        Assert.True(PageLayout.Widths(width, hasRepo: true, hasAction: true)[4] > 0);
        Assert.True(PageLayout.Widths(width, hasRepo: false, hasAction: false)[4] > 0);
    }

    [Fact]
    public void Stage_stays_while_the_columns_fit_the_table_width()
    {
        // 160 (title) + 130 (status) + 110 (stage) + 70 (age) + 260 (action) = 730: a 950 px table has room for all of them.
        Assert.True(PageLayout.Widths(950, hasRepo: false, hasAction: true)[3] > 0);
        // With no row offering an action there is no action column at all.
        Assert.Equal(0d, PageLayout.Widths(950, hasRepo: false, hasAction: false)[5]);
    }

    [Fact]
    public void A_table_too_narrow_drops_the_empty_repo_first_then_stage_then_repo_and_never_age()
    {
        Assert.Equal(0d, PageLayout.Widths(1400, hasRepo: false, hasAction: true)[1]);
        Assert.True(PageLayout.Widths(1400, hasRepo: true, hasAction: true)[1] > 0);

        var tight = PageLayout.Widths(800, hasRepo: true, hasAction: true);   // 160+150+130+110+70+260 = 880 > 800: Stage goes, 770 fits
        Assert.Equal((true, 0d), (tight[1] > 0, tight[3]));

        var tighter = PageLayout.Widths(700, hasRepo: true, hasAction: true);   // 770 > 700: Repo goes too
        Assert.Equal((0d, 0d, true), (tighter[1], tighter[3], tighter[4] > 0));
    }
}
