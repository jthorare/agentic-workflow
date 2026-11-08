using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Items;

/// <summary>Folds 'item.linked' into the 'item-links' view: one row per thing an item is linked to (issue, pull request; runs come later). Red stub: Apply is not written yet.</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Red stub: registered in SindriCoreServiceCollectionExtensions by the green commit; Aw.Sindri.Tests uses it through InternalsVisibleTo.")]
internal sealed class ItemLinksProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.ItemLinks];

    public bool Handles(string eventType) => string.Equals(eventType, EventTypes.ItemLinked, StringComparison.Ordinal);

    public ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        throw new NotSupportedException("ItemLinksProjection.Apply is written in the green commit.");
    }
}
