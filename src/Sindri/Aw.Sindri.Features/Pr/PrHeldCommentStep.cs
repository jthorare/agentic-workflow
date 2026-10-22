using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>'step.pr.held-comment' on the block runtime (babysitter spec §5.3): the oldest comment marked 'held' before the owner's walk started, as 'fact:comment'; an empty walk is 'drainedUnpushed' when the workspace holds unpushed commits, else 'drained'.</summary>
internal sealed class PrHeldCommentStep(IPrCommentMarks marks) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.pr.held-comment");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var current = await PrCommentQueue.CurrentMarks(context, marks, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return BlockStepResult.Failed($"step.pr.held-comment can't read the comment marks: {current.Error.Message}");
        }

        if (PrCommentQueue.Parse(StepFacts.Read(context, "fact:prState"), current.Value) is not { } queue)
        {
            return BlockStepResult.Failed("step.pr.held-comment needs fact:prState, an object, which the instance does not hold");
        }

        return queue.NextHeld() is { } entry
            ? StepFacts.Done("comment", ("fact:comment", PrCommentQueue.ToFact(entry)))
            : StepFacts.Done(queue.WorkspaceAhead ? "drainedUnpushed" : "drained");
    }
}
