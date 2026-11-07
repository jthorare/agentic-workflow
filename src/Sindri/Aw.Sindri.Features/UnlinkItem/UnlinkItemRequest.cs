using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.UnlinkItem;

/// <summary>'sindri item unlink &lt;item&gt;' (Linear hub spec 2026-11-04 §6.1; owner only): appends 'item.unlinked'.</summary>
/// <param name="Item">The item, by id or alias.</param>
internal sealed record UnlinkItemRequest(ItemRef Item) : ICommand<UnlinkItemResponse>;
