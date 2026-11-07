using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.LinkItem;

/// <summary>'sindri item link &lt;item&gt; &lt;issue&gt;' (Linear hub spec 2026-11-04 §6.1; owner only): appends 'item.linked' with the issue's record key and alias.</summary>
/// <param name="Item">The item, by id or alias.</param>
/// <param name="Key">The issue's human key, 'ENG-12'.</param>
internal sealed record LinkItemRequest(ItemRef Item, string Key) : ICommand<LinkItemResponse>;
