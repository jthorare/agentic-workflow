using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Queue;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>The Work page's fold arrows (#1082): down while a group is open, right while it is folded, for each group and for Closed.</summary>
[Lane(TestLane.Unit)]
public sealed class FoldGlyphTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private QueueViewModel Queue()
    {
        _server.ListItemsBody = _ =>
        {
            var list = new ItemListView([], Empty: null, new AsOfView(1, 1, A.Noon, Rebuilding: false, Stalled: false));
            return new RouteAnswer(HttpStatusCode.OK, JsonSerializer.Serialize(list, SindriApiClient.Json));
        };
        return new QueueViewModel(_server, new CatalogViewModel(new FakeClipboard(), _clock), _clock);
    }

    [Fact]
    public void A_group_shows_down_while_open_and_right_once_folded_and_says_so()
    {
        var group = new QueueGroupViewModel("queued");
        var changed = new List<string?>();
        group.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal(Labels.FoldOpen, group.FoldGlyph);

        group.IsExpanded = false;
        Assert.Equal(Labels.FoldFolded, group.FoldGlyph);
        Assert.Contains(nameof(QueueGroupViewModel.FoldGlyph), changed, StringComparer.Ordinal);

        group.IsExpanded = true;
        Assert.Equal(Labels.FoldOpen, group.FoldGlyph);
    }

    [Fact]
    public async Task The_Closed_header_shows_right_until_opened_then_down_and_says_so()
    {
        var queue = Queue();
        var changed = new List<string?>();
        queue.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.Equal(Labels.FoldFolded, queue.ClosedArrow);

        await queue.ToggleClosed(_api, Token);
        Assert.Equal(Labels.FoldOpen, queue.ClosedArrow);
        Assert.Contains(nameof(QueueViewModel.ClosedArrow), changed, StringComparer.Ordinal);

        await queue.ToggleClosed(_api, Token);
        Assert.Equal(Labels.FoldFolded, queue.ClosedArrow);
    }
}
