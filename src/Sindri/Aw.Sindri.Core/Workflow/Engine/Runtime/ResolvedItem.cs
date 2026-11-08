using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>An item as <see cref="IItems"/> resolved it: its id and its tracker-key aliases (engine spec rev 8 §6.1).</summary>
/// <param name="Id">The item's id ('ITM-…').</param>
/// <param name="Aliases">Its tracker keys ('T-3').</param>
public sealed record ResolvedItem(ItemKey Id, IReadOnlyList<ItemKey> Aliases);
