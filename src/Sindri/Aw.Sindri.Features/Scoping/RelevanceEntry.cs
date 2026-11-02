namespace Aw.Sindri.Features.Scoping;

/// <summary>One thing a lens returned: a node (it has a graph or file key) or an item (a record, decision or commit; no key).</summary>
/// <param name="Id">The lens item id, 'L-&lt;lens&gt;-&lt;n&gt;'.</param>
/// <param name="Lens">The lens it was found by.</param>
/// <param name="Key">The node key; null for an item.</param>
/// <param name="Kind">What it is.</param>
/// <param name="Label">Its label.</param>
/// <param name="Band">The node's band; null for an item.</param>
internal sealed record RelevanceEntry(string Id, string Lens, string? Key, string Kind, string Label, string? Band);
