using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;

namespace Aw.Sindri.Core.Views;

/// <summary>A row of the 'item-links' view: one thing an item is linked to (a tracker issue, a pull request, a graph run).</summary>
/// <param name="Item">The item.</param>
/// <param name="Kind">What it is linked to.</param>
/// <param name="Ref">The link's short name: the tracker key ('ENG-142'), the record key, or the run id.</param>
/// <param name="Url">Where it lives, or null when the link has no page.</param>
/// <param name="State">The linked thing's state in its own words, or null when unknown.</param>
/// <param name="UpdatedAt">When the link last changed.</param>
/// <param name="Sequence">The ledger sequence that last changed the row.</param>
public sealed record ItemLinkRow(ItemId Item, ItemLinkKind Kind, string Ref, string? Url, string? State, DateTimeOffset UpdatedAt, long Sequence);
