using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Runs a block-runtime Step by its id, in a scope of its own (DT spec §8.2); registered by AddSindriWorkflows over the AddBlockStep registrations.</summary>
/// <param name="step">The Step's id.</param>
/// <param name="context">What it runs with.</param>
/// <param name="cancellationToken">Cancelled when the activity is.</param>
/// <returns>What the Step returned.</returns>
internal delegate ValueTask<BlockStepResult> BlockStepDispatch(BlockId step, StepContext context, CancellationToken cancellationToken);
