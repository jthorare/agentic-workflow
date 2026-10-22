using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// 'step.pr.next-comment' on the block runtime (babysitter spec §5.2): the oldest unhandled thread of the snapshot's queue, as 'fact:comment'. Authorship is checked before
/// content: 'untrusted', then 'bot', then 'comment'; an empty queue is 'drainedUnpushed' when the workspace holds commits the PR head lacks, else 'drained'.
/// </summary>
/// <param name="marks">The comment marks: what was handled since the snapshot.</param>
internal sealed class PrNextCommentStep(IPrCommentMarks marks) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.pr.next-comment");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var current = await PrCommentQueue.CurrentMarks(context, marks, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return BlockStepResult.Failed($"step.pr.next-comment can't read the comment marks: {current.Error.Message}");
        }

        if (PrCommentQueue.Parse(StepFacts.Read(context, "fact:prState"), current.Value) is not { } queue)
        {
            return BlockStepResult.Failed("step.pr.next-comment needs fact:prState, an object, which the instance does not hold");
        }

        if (queue.NextUnhandled() is not { } entry)
        {
            return StepFacts.Done(queue.WorkspaceAhead ? "drainedUnpushed" : "drained");
        }

        var bots = context.Arguments.TryGetValue("bots", out var argument) ? PrCommentQueue.Bots(JsonNode.Parse(argument.GetRawText())) : [];
        return StepFacts.Done(PrCommentQueue.Classify(entry, bots), ("fact:comment", PrCommentQueue.ToFact(entry)));
    }
}
