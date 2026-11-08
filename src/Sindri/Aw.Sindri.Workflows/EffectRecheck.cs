using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the 'RecheckEffect' local activity checks (engine spec §5.7; engine P6 unit (b)).</summary>
/// <param name="Recipe">The instance's pinned recipe: its approval must still hold.</param>
/// <param name="Kind">The effect block: it must still be on the approved profile's 'workflow.effects'.</param>
/// <param name="Origin">An engine-origin effect is exempt from both checks.</param>
internal sealed record EffectRecheck(RecipeVersion Recipe, BlockId Kind, CommandOrigin Origin);
