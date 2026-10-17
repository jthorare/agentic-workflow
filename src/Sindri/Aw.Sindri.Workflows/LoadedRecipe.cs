using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What the 'LoadRecipe' local activity hands the interpreter (engine spec rev 8 §5.1): the approved recipe's version,
/// its normalized long form, and the descriptors of the blocks it uses, so every decision reads them from history and
/// stays deterministic.
/// </summary>
/// <param name="Version">The id, version and approved hash.</param>
/// <param name="Recipe">The recipe's long form, its <see cref="Recipe.Hash"/> set.</param>
/// <param name="Blocks">The build's descriptors of the blocks the recipe names.</param>
/// <param name="DeskTimeout">The 'workflow.deskTimeout' floor every desk wait is raised to (W20; engine P4 unit (c)).</param>
/// <param name="EventHold">'workflow.eventHold': how long a delivered event waits for a wait before it expires (§5.4).</param>
/// <param name="DispatchDelay">'workflow.dispatchDelay': the undo window before each effect's activity (§5.7; engine P6).</param>
/// <param name="BlockRuntimes">The recipe's Steps this build runs on the block runtime (DT-J2). Null in a marker written before it; the interpreter reads it only on the patched path, so replay stays the same.</param>
internal sealed record LoadedRecipe(RecipeVersion Version, Recipe Recipe, IReadOnlyList<BlockDescriptor> Blocks, TimeSpan DeskTimeout, TimeSpan EventHold, TimeSpan DispatchDelay, IReadOnlyList<BlockId>? BlockRuntimes = null);
