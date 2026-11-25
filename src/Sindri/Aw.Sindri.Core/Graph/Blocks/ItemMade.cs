using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What <see cref="ICreateItemPort"/> made.</summary>
/// <param name="ItemId">The new item's id.</param>
/// <param name="Warning">What was not written after the item was made (the item exists; a node must not fail and make a second one), in plain words; null when everything was written.</param>
public sealed record ItemMade(ItemId ItemId, string? Warning);
