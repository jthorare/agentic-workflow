using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>'step.pr.held-comment' on the block runtime (babysitter spec §5.3): the oldest comment marked 'held' before the owner's walk started, as 'fact:comment'; an empty walk is 'drainedUnpushed' when the workspace holds unpushed commits, else 'drained'.</summary>
internal sealed class PrHeldCommentStep : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.pr.held-comment");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (PrCommentQueue.Parse(StepFacts.Read(context, "fact:prState")) is not { } queue)
        {
            return ValueTask.FromResult(BlockStepResult.Failed("step.pr.held-comment needs fact:prState, an object, which the instance does not hold"));
        }

        return ValueTask.FromResult(queue.NextHeld() is { } entry
            ? StepFacts.Done("comment", ("fact:comment", PrCommentQueue.ToFact(entry)))
            : StepFacts.Done(queue.WorkspaceAhead ? "drainedUnpushed" : "drained"));
    }
}
