using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.LinearHubPh4;

/// <summary>The link folds (Linear hub spec 2026-11-04 §6.1): ItemCreated.Link makes an alias row, 'item.unlinked' removes the item's link and the alias row.</summary>
public sealed class ItemLinkFoldTests
{
    private static readonly RecordKey _record = RecordKey.From("linear:issue/00000000-0000-4000-8000-000000000112");
    private static readonly ItemAlias _alias = ItemAlias.From("ENG-12");
    private readonly SliceHarness _h = new();
    private readonly ItemId _item = ItemId.From("ITM-" + new string('1', 32));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ItemCreated CreatedPayload(CallerClass? by = CallerClass.Human, ItemAlias? alias = null) =>
        new(_h.Ids.New<CommandId>(), _item, "Sync drops rows", Repo: null, _record, Body: null, Owner: null, by, alias ?? _alias);

    private LedgerEvent Created(long sequence) => Recorded.Of(_h.Events, EventTypes.ItemCreated, CreatedPayload(), sequence, _item);

    private LedgerEvent Unlinked(long sequence) =>
        Recorded.Of(_h.Events, EventTypesPh4.ItemUnlinked, new ItemUnlinked(_h.Ids.New<CommandId>(), _item, _record, _alias, CallerClass.Human), sequence, _item);

    private async Task<InMemoryViewStore> Fold(params LedgerEvent[] page)
    {
        var scrubber = new PassThroughScrubber();
        var views = new InMemoryViewStore(SliceHarness.Json);
        _ = await views.Open(ViewSchema.Version, Token);
        IProjection[] projections = [new ItemsProjection(_h.Events, scrubber), new ItemAliasesProjection(_h.Events), new ItemTimelineProjection(SliceHarness.Json, scrubber)];
        var applied = (await views.Apply(page, projections, _h.Clock.GetUtcNow(), Token)).Value;
        Assert.Null(applied.Stall);
        return views;
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task An_item_created_with_a_link_and_alias_makes_the_alias_row()
    {
        var views = await Fold(Created(1));

        var row = (await views.Get<AliasRow>(ViewNames.ItemAliases, _alias.Value, Token)).Value;

        Assert.Equal((_item, _record), (row!.Item, row.Record));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task An_item_unlinked_clears_the_items_link_and_alias()
    {
        var views = await Fold(Created(1), Unlinked(2));

        var item = (await views.Get<ItemRow>(ViewNames.Items, _item.Value, Token)).Value!;

        Assert.Null(item.Link);
        Assert.Null(item.Alias);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task An_item_unlinked_removes_the_alias_row()
    {
        var views = await Fold(Created(1), Unlinked(2));

        Assert.Null((await views.Get<AliasRow>(ViewNames.ItemAliases, _alias.Value, Token)).Value);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task An_item_unlinked_adds_a_timeline_line()
    {
        var views = await Fold(Created(1), Unlinked(2));

        var lines = (await views.Range<TimelineRow>(ViewNames.ItemTimeline, afterKey: _item.Value + "/", beforeKey: _item.Value + "0", max: 10, Token)).Value;

        Assert.Equal("item.unlinked", lines[^1].Type);
        Assert.Equal("Unlinked from linear:issue/00000000-0000-4000-8000-000000000112 (ENG-12)", lines[^1].Line);
    }
}
