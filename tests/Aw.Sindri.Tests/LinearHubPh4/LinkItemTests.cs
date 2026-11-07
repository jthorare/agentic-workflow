using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.LinkItem;
using Aw.Sindri.Features.UnlinkItem;

namespace Aw.Sindri.Tests.LinearHubPh4;

/// <summary>'item link' and 'item unlink' (Linear hub spec 2026-11-04 §6.1, D14, TP-8): the owner links an item to a stored issue, one open item holds an issue, and unlink frees it.</summary>
public sealed class LinkItemTests
{
    private readonly Ph4Rig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // red: step 2 passes the ports to the constructors (facts, item resolver, items view, ledger); the stubs take none yet
    private LinkItemService Link()
    {
        ArgumentNullException.ThrowIfNull(_rig.Facts);
        return new LinkItemService();
    }

    private static UnlinkItemService Unlink() => new();

    [Fact, Lane(TestLane.Unit)]
    public async Task Link_appends_item_linked_with_the_record_the_alias_and_the_owner()
    {
        await _rig.Hold();
        var item = await _rig.NewItem();
        using var entered = _rig.Owner();

        var linked = await Link().Handle(new LinkItemRequest(ItemRef.To(item), Ph4Rig.Key), Token);

        Assert.True(linked.IsSuccess, linked.IsError ? linked.Error.ToString() : null);
        var payload = _rig.H.Events.Payload<ItemLinked>(Assert.Single(_rig.Events(EventTypes.ItemLinked))).Value;
        Assert.Equal((item, _rig.Record, Ph4Rig.Alias, CallerClass.Human), (payload.Item, payload.Record, payload.Alias!.Value, payload.By!.Value));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task A_sessions_link_is_refused_SND_AUTH_002_and_appends_nothing()
    {
        await _rig.Hold();
        var item = await _rig.NewItem();
        using var entered = _rig.Session();

        var linked = await Link().Handle(new LinkItemRequest(ItemRef.To(item), Ph4Rig.Key), Token);

        Assert.True(linked.IsError);
        Assert.Equal(SindriErrors.AuthRefused.Code, linked.Error.Code);
        Assert.Empty(_rig.Events(EventTypes.ItemLinked));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task Linking_a_second_item_to_a_held_issue_is_SND_LINEAR_007()
    {
        await _rig.Hold();
        var first = await _rig.NewItem("one");
        var second = await _rig.NewItem("two");
        using var entered = _rig.Owner();
        _ = await Link().Handle(new LinkItemRequest(ItemRef.To(first), Ph4Rig.Key), Token);

        var again = await Link().Handle(new LinkItemRequest(ItemRef.To(second), Ph4Rig.Key), Token);

        Assert.True(again.IsError);
        Assert.Equal(("SND-LINEAR-007", ErrorKind.Conflict), (again.Error.Code, again.Error.Kind));
        Assert.Contains($"ENG-12 is linked to {first.Value} (", again.Error.Message, StringComparison.Ordinal);
        Assert.Contains("Open it, or unlink it first.", again.Error.Message, StringComparison.Ordinal);
        _ = Assert.Single(_rig.Events(EventTypes.ItemLinked));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task Two_racing_links_on_one_issue_leave_one()
    {
        await _rig.Hold();
        var first = await _rig.NewItem("one");
        var second = await _rig.NewItem("two");
        using var entered = _rig.Owner();

        var results = await Task.WhenAll(
            Link().Handle(new LinkItemRequest(ItemRef.To(first), Ph4Rig.Key), Token).AsTask(),
            Link().Handle(new LinkItemRequest(ItemRef.To(second), Ph4Rig.Key), Token).AsTask());

        _ = Assert.Single(results, static r => r.IsSuccess);
        _ = Assert.Single(results, static r => r.IsError && r.Error.Code == "SND-LINEAR-007");
        _ = Assert.Single(_rig.Events(EventTypes.ItemLinked));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task Link_then_unlink_then_link_works_and_the_unlink_names_the_record_and_alias()
    {
        await _rig.Hold();
        var first = await _rig.NewItem("one");
        var second = await _rig.NewItem("two");
        using var entered = _rig.Owner();
        _ = await Link().Handle(new LinkItemRequest(ItemRef.To(first), Ph4Rig.Key), Token);

        var unlinked = await Unlink().Handle(new UnlinkItemRequest(ItemRef.To(first)), Token);
        var relinked = await Link().Handle(new LinkItemRequest(ItemRef.To(second), Ph4Rig.Key), Token);

        Assert.True(unlinked.IsSuccess, unlinked.IsError ? unlinked.Error.ToString() : null);
        Assert.True(relinked.IsSuccess, relinked.IsError ? relinked.Error.ToString() : null);
        var gone = _rig.H.Events.Payload<ItemUnlinked>(Assert.Single(_rig.Events(EventTypesPh4.ItemUnlinked))).Value;
        Assert.Equal((first, _rig.Record, Ph4Rig.Alias, CallerClass.Human), (gone.Item, gone.Record, gone.Alias!.Value, gone.By));
        Assert.Equal(2, _rig.Events(EventTypes.ItemLinked).Count);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task A_sessions_unlink_is_refused_SND_AUTH_002()
    {
        await _rig.Hold();
        var item = await _rig.NewItem();
        using var entered = _rig.Session();

        var unlinked = await Unlink().Handle(new UnlinkItemRequest(ItemRef.To(item)), Token);

        Assert.True(unlinked.IsError);
        Assert.Equal(SindriErrors.AuthRefused.Code, unlinked.Error.Code);
        Assert.Empty(_rig.Events(EventTypesPh4.ItemUnlinked));
    }
}
