using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.gap-log' on the block runtime: <see cref="GapLog"/> over the facts the Step reads. 'clear' writes the record, no anchors and the latch; 'confirmed' writes the record, the anchors
/// to look at again and the findings.
/// </summary>
internal sealed class GapLogStep : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.gap-log");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Missing(context, "fact:gapSearch", "fact:gapCandidates", "fact:gapRulings") is { Count: > 0 } missing)
        {
            return ValueTask.FromResult(BlockStepResult.Failed($"{Id.Value} needs {string.Join(", ", missing)}, which the instance does not hold"));
        }

        var applied = GapLog.Apply(StepFacts.Read(context, "fact:gapSearch"), StepFacts.Read(context, "fact:gapCandidates"), StepFacts.Read(context, "fact:gapRulings"), StepFacts.Read(context, "fact:findings"));
        if (applied.IsError)
        {
            return ValueTask.FromResult(BlockStepResult.Failed(applied.Error.Message));
        }

        var round = applied.Value;
        return ValueTask.FromResult(round.Findings is { } findings
            ? StepFacts.Done(round.Outcome, ("fact:gapSearch", round.GapSearch), ("fact:gapAnchors", round.GapAnchors), ("fact:findings", findings))
            : StepFacts.Done(round.Outcome, ("fact:gapSearch", round.GapSearch), ("fact:gapAnchors", round.GapAnchors), ("fact:gapSearchClosed", round.Closed!)));   // why: Apply sets the latch exactly when there are no findings
    }
}
