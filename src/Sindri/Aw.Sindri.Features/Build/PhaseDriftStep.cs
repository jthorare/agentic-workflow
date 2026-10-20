using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Build;

/// <summary>'step.phase.drift' on the block runtime (line spec §7.1): 'same' when review left the phase worktree's head at the proven one, 'moved' with the new built head and change when it did not.</summary>
/// <param name="diff">Reads the worktree's HEAD.</param>
internal sealed class PhaseDriftStep(IRunDiff diff) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.phase.drift");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var worktree = StepFacts.Text(context, "fact:workspace");
        var proven = StepFacts.Head(StepFacts.Read(context, "fact:built"));
        if (worktree is null || proven is null)
        {
            return BlockStepResult.Failed("step.phase.drift needs fact:workspace (a path) and fact:built (with its head)");
        }

        var head = await diff.Head(worktree, cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return BlockStepResult.Failed($"step.phase.drift can't read the head of {worktree}: {head.Error.Message}");
        }

        if (string.Equals(head.Value, proven, StringComparison.Ordinal))
        {
            return StepFacts.Done("same");
        }

        return StepFacts.Done("moved", ("fact:built", WithHead(StepFacts.Read(context, "fact:built"), head.Value)), ("fact:change", WithHead(StepFacts.Read(context, "fact:change"), head.Value)));
    }

    private static JsonObject WithHead(JsonNode? fact, string head)
    {
        var copy = fact is JsonObject obj ? (JsonObject)obj.DeepClone() : [];
        copy["head"] = head;
        return copy;
    }
}
