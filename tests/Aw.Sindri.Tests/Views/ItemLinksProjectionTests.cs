using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The 'item-links' fold (Sindri v2 PR 4b): 'item.linked' becomes one row per thing an item is linked to.</summary>
public sealed class ItemLinksProjectionTests
{
    private static readonly RecordKey _issue = RecordKey.From("linear:issue/abc-123");
    private static readonly RecordKey _pr = RecordKey.From("github:pr/acme-widgets-12");
    private static readonly ItemAlias _alias = ItemAlias.From("ENG-142");
    private readonly SliceHarness _h = new();
    private readonly InMemoryViewStore _views = new(SliceHarness.Json);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_linked_issue_and_PR_show_as_links_on_the_item_row()
    {
        await Link(_issue, _alias, sequence: 1);
        await Link(_pr, alias: null, sequence: 2);

        var rows = (await _views.List<ItemLinkRow>(ViewNames.ItemLinks, Token)).Value.OrderBy(static r => r.Sequence).ToList();

        Assert.Equal(
            [(A.FirstItemId, ItemLinkKind.Issue, "ENG-142", 1L), (A.FirstItemId, ItemLinkKind.Pr, _pr.Value, 2L)],
            rows.Select(static r => (r.Item, r.Kind, r.Ref, r.Sequence)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Linking_the_same_record_twice_keeps_one_row()
    {
        await Link(_issue, _alias, sequence: 1);
        _h.Clock.Advance(TimeSpan.FromMinutes(5));
        await Link(_issue, _alias, sequence: 2);

        var row = Assert.Single((await _views.List<ItemLinkRow>(ViewNames.ItemLinks, Token)).Value);

        Assert.Equal((2L, _h.Clock.GetUtcNow()), (row.Sequence, row.UpdatedAt));
    }

    private async Task Link(RecordKey record, ItemAlias? alias, long sequence)
    {
        _ = (await _views.Open(ViewSchema.Version, Token)).Value;
        var linked = Recorded.Of(_h.Events, EventTypes.ItemLinked, new ItemLinked(_h.Ids.New<CommandId>(), A.FirstItemId, record, alias), sequence, A.FirstItemId);
        var applied = (await _views.Apply([linked], [new ItemLinksProjection(_h.Events)], _h.Clock.GetUtcNow(), Token)).Value;
        Assert.Null(applied.Stall);
    }
}
