using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The items, item-aliases and item-timeline folds (API spec §3.1): pure rows from ledger events, text scrubbed and escaped.</summary>
public sealed class ItemProjectionTests
{
    private static readonly RecordKey _record = RecordKey.From("linear:issue/abc-123");
    private static readonly ItemAlias _alias = ItemAlias.From("ENG-142");
    private static readonly PassThroughScrubber _plain = new();
    private readonly SliceHarness _h = new();
    private readonly ItemId _item = ItemId.From("ITM-" + new string('1', 32));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public void Created_item_is_new_at_version_1()
    {
        var recorded = Created("Ship it", 1, link: _record, body: "why");

        var row = ItemsProjection.Fold(CreatedPayload("Ship it", _record, "why"), recorded, _plain);

        Assert.Equal((_item, "Ship it", ItemState.New, 1L, _record, null, "why"), (row.Id, row.Title, row.State, row.Version, row.Link, row.Alias, row.Body));
        Assert.Equal((recorded.Envelope.OccurredAt, recorded.Envelope.OccurredAt, recorded.Envelope.OccurredAt, recorded.Envelope.Actor, 1L), (row.CreatedAt, row.Since, row.UpdatedAt, row.CreatedBy, row.Sequence));
        Assert.Null(row.Step);
        Assert.Null(row.WaitingOn);
        Assert.Null(row.Instance);
        Assert.Null(row.Owner);
    }

    [Fact, Trait("Lane", "unit")]
    public void State_change_moves_state_version_and_since()
    {
        var row = ItemsProjection.Fold(CreatedPayload("t", link: null, body: null), Created("t", 1), _plain);
        _h.Clock.Advance(TimeSpan.FromMinutes(5));
        var closed = Recorded.Of(_h.Events, EventTypes.ItemStateChanged, ChangedPayload(ItemState.New, ItemState.Done, 2), 2, _item);

        var next = ItemsProjection.Fold(row, ChangedPayload(ItemState.New, ItemState.Done, 2), closed);

        Assert.Equal((ItemState.Done, 2L, closed.Envelope.OccurredAt, closed.Envelope.OccurredAt, 2L), (next.State, next.Version, next.Since, next.UpdatedAt, next.Sequence));
        Assert.Equal((row.CreatedAt, row.Title), (next.CreatedAt, next.Title));
    }

    [Fact, Trait("Lane", "unit")]
    public void Link_sets_link_and_alias()
    {
        var row = ItemsProjection.Fold(CreatedPayload("t", link: null, body: null), Created("t", 1), _plain);
        var linked = Recorded.Of(_h.Events, EventTypes.ItemLinked, new ItemLinked(_h.Ids.New<CommandId>(), _item, _record, _alias), 2, _item);

        var next = ItemsProjection.Fold(row, new ItemLinked(_h.Ids.New<CommandId>(), _item, _record, _alias), linked);

        Assert.Equal((_record, _alias, 2L), (next.Link, next.Alias, next.Sequence));
        Assert.Equal((row.State, row.Version, row.Since), (next.State, next.Version, next.Since));
    }

    [Fact, Trait("Lane", "unit")]
    public void Title_is_scrubbed_and_escaped()
    {
        var created = CreatedPayload("Fix \u001b[31mSECRET\u001b[0m\nnow", link: null, body: "line1\nSECRET​");

        var row = ItemsProjection.Fold(created, Created("x", 1), new MaskScrubber("SECRET"));

        Assert.Equal("Fix \\x1B[31m[masked]\\x1B[0m now", row.Title);
        Assert.Equal("line1\n[masked]\\u{200B}", row.Body);
    }

    [Fact, Trait("Lane", "unit")]
    public void Body_keeps_its_line_breaks_and_tabs_but_not_its_controls()
    {
        var created = CreatedPayload("t", link: null, body: "Steps:\n\n1.\tOpen\r\n2. \u001b[31mSECRET\u001b[0m\rback");

        var row = ItemsProjection.Fold(created, Created("x", 1), new MaskScrubber("SECRET"));

        Assert.Equal("Steps:\n\n1.\tOpen\n2. \\x1B[31m[masked]\\x1B[0m\\x0Dback", row.Body);
    }

    [Fact, Trait("Lane", "unit")]
    public void Title_and_body_are_cut_to_their_limits()
    {
        var row = ItemsProjection.Fold(CreatedPayload(new string('t', 500), link: null, body: new string('b', 5000)), Created("x", 1), _plain);

        Assert.Equal((200, 4000), (row.Title.Length, row.Body!.Length));
        Assert.EndsWith("…", row.Title, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Alias_moves_on_relink()
    {
        var other = ItemId.From("ITM-" + new string('2', 32));
        var first = ItemAliasesProjection.Fold(new ItemLinked(_h.Ids.New<CommandId>(), _item, _record, _alias), Recorded.Of(_h.Events, EventTypes.ItemLinked, "x", 1, _item));
        var again = ItemAliasesProjection.Fold(new ItemLinked(_h.Ids.New<CommandId>(), other, _record, _alias), Recorded.Of(_h.Events, EventTypes.ItemLinked, "x", 2, other));

        Assert.Equal((_alias, _item, _record, 1L), (first.Alias, first.Item, first.Record, first.Sequence));
        Assert.Equal((_alias, other, 2L), (again.Alias, again.Item, again.Sequence));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_relink_in_the_view_leaves_one_alias_row_naming_the_new_item()
    {
        var other = ItemId.From("ITM-" + new string('2', 32));
        var views = await Fold(
            Created("one", 1),
            Recorded.Of(_h.Events, EventTypes.ItemCreated, new ItemCreated(_h.Ids.New<CommandId>(), other, "two", Repo: null, Link: null, Body: null, Owner: null), 2, other),
            Recorded.Of(_h.Events, EventTypes.ItemLinked, new ItemLinked(_h.Ids.New<CommandId>(), _item, _record, _alias), 3, _item),
            Recorded.Of(_h.Events, EventTypes.ItemLinked, new ItemLinked(_h.Ids.New<CommandId>(), other, _record, _alias), 4, other));

        var row = (await views.Get<AliasRow>(ViewNames.ItemAliases, _alias.Value, Token)).Value;

        Assert.Equal(other, row!.Item);
        _ = Assert.Single((await views.List<AliasRow>(ViewNames.ItemAliases, Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Timeline_has_one_line_per_item_event()
    {
        var views = await Fold(
            Created("Ship it", 1),
            Recorded.Of(_h.Events, EventTypes.ItemStateChanged, ChangedPayload(ItemState.New, ItemState.Done, 2), 2, _item),
            Recorded.Of(_h.Events, EventTypes.ItemLinked, new ItemLinked(_h.Ids.New<CommandId>(), _item, _record, _alias), 3, _item));

        var lines = (await views.Range<TimelineRow>(ViewNames.ItemTimeline, afterKey: _item.Value + "/", beforeKey: _item.Value + "0", max: 10, Token)).Value;

        Assert.Equal(["Created: Ship it", "new → done", "Linked to linear:issue/abc-123 (ENG-142)"], lines.Select(l => l.Line), StringComparer.Ordinal);
        Assert.Equal([1L, 2L, 3L], lines.Select(l => l.Sequence));
        Assert.Equal(["item.created", "item.state-changed", "item.linked"], lines.Select(l => l.Type), StringComparer.Ordinal);
        Assert.Equal(ItemTimelineKeys.Of(_item.Value, 2), _item.Value + "/0000000000000000002");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_event_without_an_item_is_ignored()
    {
        var views = await Fold(Recorded.Of(_h.Events, EventTypes.ServiceRan, new ServiceRan(_h.Ids.New<CommandId>(), ServiceName.From("heartbeat"), _h.Clock.GetUtcNow(), ServiceOutcome.Ok, Code: null), 1));

        Assert.Empty((await views.List<TimelineRow>(ViewNames.ItemTimeline, Token)).Value);
        Assert.Empty((await views.List<ItemRow>(ViewNames.Items, Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Replaying_the_same_events_gives_the_same_rows()
    {
        LedgerEvent[] page =
        [
            Created("Ship it", 1, link: _record),
            Recorded.Of(_h.Events, EventTypes.ItemLinked, new ItemLinked(_h.Ids.New<CommandId>(), _item, _record, _alias), 2, _item),
            Recorded.Of(_h.Events, EventTypes.ItemStateChanged, ChangedPayload(ItemState.New, ItemState.Cancelled, 2), 3, _item),
        ];
        var views = await Fold(page);
        var first = await Snapshot(views);

        _ = await views.Reset(ViewNames.All, ViewSchema.Version, _h.Clock.GetUtcNow(), 0, Token);
        _ = await views.Apply(page, Projections(_plain), _h.Clock.GetUtcNow(), Token);

        Assert.Equal(first, await Snapshot(views));
        Assert.Contains("\"state\":\"cancelled\"", first, StringComparison.Ordinal);
        Assert.Contains("ENG-142", first, StringComparison.Ordinal);
    }

    private static async Task<string> Snapshot(InMemoryViewStore views)
    {
        var items = (await views.List<ItemRow>(ViewNames.Items, Token)).Value;
        var aliases = (await views.List<AliasRow>(ViewNames.ItemAliases, Token)).Value;
        var timeline = (await views.List<TimelineRow>(ViewNames.ItemTimeline, Token)).Value;
        return string.Join('\n', items.Select(Text).Concat(aliases.Select(Text)).Concat(timeline.Select(Text)));
    }

    private static string Text<TRow>(TRow row) => System.Text.Encoding.UTF8.GetString(Kernel.Json.AwJson.Serialize(row, SliceHarness.Json));

    private IProjection[] Projections(ITextScrubber scrubber) =>
        [new ItemsProjection(_h.Events, scrubber), new ItemAliasesProjection(_h.Events), new ItemTimelineProjection(SliceHarness.Json, scrubber)];

    private async Task<InMemoryViewStore> Fold(params LedgerEvent[] page)
    {
        var views = new InMemoryViewStore(SliceHarness.Json);
        _ = await views.Open(ViewSchema.Version, Token);
        var applied = (await views.Apply(page, Projections(_plain), _h.Clock.GetUtcNow(), Token)).Value;
        Assert.Null(applied.Stall);
        return views;
    }

    private ItemCreated CreatedPayload(string title, RecordKey? link, string? body) =>
        new(_h.Ids.New<CommandId>(), _item, title, Repo: null, link, body, Owner: null);

    private ItemStateChanged ChangedPayload(ItemState from, ItemState to, long version) =>
        new(_h.Ids.New<CommandId>(), _item, from, to, version, Reason: null);

    private LedgerEvent Created(string title, long sequence, RecordKey? link = null, string? body = null) =>
        Recorded.Of(_h.Events, EventTypes.ItemCreated, CreatedPayload(title, link, body), sequence, _item);

    private sealed class MaskScrubber(string secret) : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(secret, "[masked]", StringComparison.Ordinal);
    }
}
