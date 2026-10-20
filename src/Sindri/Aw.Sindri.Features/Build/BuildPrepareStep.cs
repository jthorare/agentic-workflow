using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Build;

/// <summary>
/// 'step.build.prepare' on the block runtime (line spec §7.2): the phase worktree must be clean at the head it stands on, and the Step seeds
/// the build's facts (the packet in force is the phase packet; no red tests, no built head, no findings or answers yet). A dirty or missing
/// worktree fails the Step with the reason, so the instance parks instead of building on someone's half-done work.
/// </summary>
/// <param name="diff">Reads the worktree's HEAD and whether it holds changes.</param>
internal sealed class BuildPrepareStep(IRunDiff diff) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.build.prepare");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var worktree = StepFacts.Text(context, "fact:workspace");
        if (worktree is null || StepFacts.Read(context, "fact:phasePacket") is not { } packet)
        {
            return BlockStepResult.Failed("step.build.prepare needs fact:workspace (a path) and fact:phasePacket");
        }

        var head = await diff.Head(worktree, cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return BlockStepResult.Failed($"step.build.prepare can't read {worktree}: {head.Error.Message}");
        }

        var clean = await diff.IsEmpty(worktree, head.Value, cancellationToken).ConfigureAwait(false);
        if (clean.IsError)
        {
            return BlockStepResult.Failed($"step.build.prepare can't read {worktree}: {clean.Error.Message}");
        }

        return clean.Value
            ? StepFacts.Done("ready", ("fact:activePacket", packet.DeepClone()), ("fact:redTests", new JsonObject()), ("fact:built", new JsonObject()), ("fact:change", new JsonObject()), ("fact:findings", new JsonArray()), ("fact:answers", new JsonArray()))
            : BlockStepResult.Failed($"step.build.prepare: the worktree {worktree} has uncommitted changes (fix: commit or discard them, then resume)");
    }
}
