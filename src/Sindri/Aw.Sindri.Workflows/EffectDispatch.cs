using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Runs the runtime of the effect block <see cref="EffectContext.Effect"/> names (spec §2, §5.7; engine P6 unit (a)),
/// in a DI scope of its own: blocks are scoped and 'RunEffect' is a singleton. Registered in
/// <see cref="SindriWorkflowsServiceCollectionExtensions.AddSindriWorkflows"/> over the blocks AddEffect keys by id.
/// </summary>
/// <param name="context">The effect.</param>
/// <param name="cancellationToken">The activity's token.</param>
/// <returns>What the block did, or its error.</returns>
internal delegate ValueTask<Result<EffectDone>> EffectDispatch(EffectContext context, CancellationToken cancellationToken);
