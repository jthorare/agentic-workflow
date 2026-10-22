using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// 'step.line.seed' on the block runtime (line spec §3.5, LN-WF1): after the plan is frozen, writes the line's phase board, one <c language="text">waiting</c> row per phase of
/// <c language="text">fact:phasePackets</c> in plan order, and the empty <c language="text">fact:phaseEvent</c> and <c language="text">fact:boardEdit</c> the dispatcher folds. A line with no phases is a failed step: there is nothing to dispatch.
/// </summary>
internal sealed class LineSeedStep : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.line.seed");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var board = PhaseBoard.Of(StepFacts.Read(context, "fact:phasePackets"));
        return ValueTask.FromResult(board.Rows.Count == 0
            ? BlockStepResult.Failed($"{Id.Value}: fact:phasePackets lists no phase to dispatch")
            : StepFacts.Done("seeded", ("fact:phaseBoard", board.ToJson()), ("fact:phaseEvent", new JsonObject()), ("fact:boardEdit", new JsonObject())));
    }
}
