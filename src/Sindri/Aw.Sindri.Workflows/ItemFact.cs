namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The stored content of 'fact:item' (engine spec rev 8 §12): the item, the repo it works in, and its spec.</summary>
/// <param name="Id">The item's id.</param>
/// <param name="Repo">A key of the profile's repos; the 'Repo' search attribute. Optional: an item with no repo is read, and the readers that need one say so.</param>
/// <param name="Spec">The item's spec, which the router copies into 'fact:stepSpec'. Optional: a fact without one has no spec.</param>
internal sealed record ItemFact(string Id, string? Repo = null, string? Spec = null);
