using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>Folds 'item.linked' into the 'item-links' view: one row per thing an item is linked to (issue, pull request, workflow run). A record of another kind has no row.</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class ItemLinksProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.ItemLinks];

    public bool Handles(string eventType) => string.Equals(eventType, EventTypes.ItemLinked, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        var linked = events.Payload<ItemLinked>(recorded);
        if (linked.IsError)
        {
            return linked.Error;
        }

        if (KindOf(linked.Value.Record) is not { } kind)
        {
            return Unit.Value;
        }

        var row = new ItemLinkRow(linked.Value.Item, kind, linked.Value.Alias?.Value ?? linked.Value.Record.Value, linked.Value.Url, State: null, recorded.Envelope.OccurredAt, recorded.Sequence);
        return await rows.Put(ViewNames.ItemLinks, linked.Value.Item.Value + "/" + KeyWord(kind) + "/" + linked.Value.Record.Value, row, cancellationToken).ConfigureAwait(false);
    }

    private static string KeyWord(ItemLinkKind kind) => kind switch
    {
        ItemLinkKind.Issue => "issue",
        ItemLinkKind.Pr => "pr",
        ItemLinkKind.Run => "run",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    // '<source>:<kind>/<id>': the middle word names what the record is
    private static ItemLinkKind? KindOf(RecordKey record)
    {
        var text = record.Value;
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        var slash = text.IndexOf('/', StringComparison.Ordinal);
        return text[(colon + 1)..slash] switch
        {
            "issue" => ItemLinkKind.Issue,
            "pr" => ItemLinkKind.Pr,
            "run" => ItemLinkKind.Run,
            _ => null,
        };
    }
}
