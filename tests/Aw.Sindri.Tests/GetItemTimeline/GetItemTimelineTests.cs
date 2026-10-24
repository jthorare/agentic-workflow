using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Features.GetItemTimeline;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Items;

namespace Aw.Sindri.Tests.GetItemTimeline;

/// <summary>An item's timeline (API spec §3.1, U2-API-4): oldest first, a page of 100 whose 'next' exists only when a 101st row does.</summary>
[Lane(TestLane.Unit)]
public sealed class GetItemTimelineTests
{
    private readonly ItemQueryHarness _q = new();

    private GetItemTimelineService Service() => new(new ViewItemResolver(_q.Views.Views), _q.Views.Views, _q.Views.Progress, _q.Views.Slice.Callers);

    [Fact]
    public async Task GetItemTimeline_lists_the_items_events_after_a_cursor()
    {
        await _q.Item('1', "Ship it");
        await _q.Item('2', "other");
        await _q.Move('1', ItemState.Running);
        await _q.Move('1', ItemState.Done);
        using var entered = _q.Human();

        var all = await Service().Handle(A.GetItemTimelineRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token);
        var first = all.Value.Entries[0].Sequence;
        var rest = await Service().Handle(A.GetItemTimelineRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).After(first).Build(), ItemQueryHarness.Token);

        Assert.True(all.IsSuccess, all.IsError ? all.Error.ToString() : null);
        Expect.Equal(3, all.Value.Entries.Count);
        Expect.Sequence([EventTypes.ItemCreated, EventTypes.ItemStateChanged, EventTypes.ItemStateChanged], [.. all.Value.Entries.Select(e => e.Type)]);
        Expect.Equal(2, rest.Value.Entries.Count);
        Assert.True(rest.Value.Entries.All(e => e.Sequence > first));
        Assert.Null(all.Value.Empty);
    }

    [Fact]
    public async Task Timeline_pages_with_next()
    {
        await _q.Item('1', "busy");
        for (var i = 0; i < 100; i++)
        {
            await _q.Move('1', i % 2 == 0 ? ItemState.Running : ItemState.Queued);
        }

        using var entered = _q.Human();
        var request = A.GetItemTimelineRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1')));

        var page = (await Service().Handle(request.Build(), ItemQueryHarness.Token)).Value;
        var last = (await Service().Handle(request.After(page.Next).Build(), ItemQueryHarness.Token)).Value;

        Expect.Equal(100, page.Entries.Count);
        Expect.Equal(page.Entries[^1].Sequence, page.Next);
        Expect.Equal(1, last.Entries.Count);
        Assert.Null(last.Next);
    }

    [Fact]
    public async Task Timeline_last_page_has_null_next()
    {
        await _q.Item('1', "short");
        using var entered = _q.Human();

        var page = (await Service().Handle(A.GetItemTimelineRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token)).Value;

        Assert.Null(page.Next);
    }

    [Fact]
    public async Task Session_outside_the_repo_reads_the_timeline()
    {
        await _q.Item('1', "secret", repo: "alpha");
        using var entered = _q.Session("beta");

        var result = await Service().Handle(A.GetItemTimelineRequest().WithItem(ItemRef.To(ItemQueryHarness.Id('1'))).Build(), ItemQueryHarness.Token);

        Assert.True(result.IsSuccess);
    }
}
