using System.Text.Json.Nodes;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Ship;

/// <summary>
/// 'step.ship.freeze' on the block runtime (line spec §7.5, SHIP-SHA-001): the hand-off draft ({ dir, head, workspace }), the proof pack ({ head }) and the review result ({ head })
/// must name one head, and it must be the phase worktree's HEAD now; then the hand-off packet is machine-frozen. Any other head is 'stale' with a SHIP-SHA-001 finding that
/// names each sha; a hand-off packet that fails its checks, or a missing fact, fails the Step.
/// </summary>
/// <param name="diff">Reads the branch head.</param>
/// <param name="packets">Freezes the hand-off packet.</param>
internal sealed class ShipFreezeStep(IRunDiff diff, IPackets packets) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.ship.freeze");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var draft = StepFacts.Read(context, "fact:handoffDraft");
        if (StepFacts.Field(draft, "dir") is not { } dir || StepFacts.Field(draft, "workspace") is not { } workspace || StepFacts.Head(draft) is not { } draftHead
            || StepFacts.Head(StepFacts.Read(context, "fact:proofPack")) is not { } proofHead || StepFacts.Head(StepFacts.Read(context, "fact:review")) is not { } reviewHead)
        {
            return BlockStepResult.Failed("step.ship.freeze needs fact:handoffDraft ({ dir, head, workspace }) and a head in fact:proofPack and fact:review");
        }

        var branch = await diff.Head(workspace, cancellationToken).ConfigureAwait(false);
        if (branch.IsError)
        {
            return BlockStepResult.Failed($"step.ship.freeze can't read the head of {workspace}: {branch.Error.Message}");
        }

        string[] heads = [proofHead, reviewHead, draftHead, branch.Value];
        return heads.All(h => string.Equals(h, branch.Value, StringComparison.Ordinal))
            ? await Freeze(dir, workspace, cancellationToken).ConfigureAwait(false)
            : Stale(heads);
    }

    private async ValueTask<BlockStepResult> Freeze(string dir, string workspace, CancellationToken cancellationToken)
    {
        var freeze = await packets.Freeze(new PacketRef(PacketKind.Handoff, dir, workspace), PacketFreezer.Machine, cancellationToken).ConfigureAwait(false);
        return freeze.IsError || !freeze.Value.Frozen || freeze.Value.Hash is null
            ? BlockStepResult.Failed($"step.ship.freeze can't freeze the hand-off in {dir}: {(freeze.IsError ? freeze.Error.Message : PacketFailure.Describe(freeze.Value.Failures))}")
            : StepFacts.Done("frozen", ("fact:handoff", new JsonObject { ["hash"] = freeze.Value.Hash, ["dir"] = dir }));
    }

    private static BlockStepResult Stale(string[] heads)
    {
        var message = $"SHIP-SHA-001: proof {heads[0]}, review {heads[1]}, hand-off {heads[2]} and the branch {heads[3]} must name one head";
        return StepFacts.Done("stale", ("fact:findings", new JsonArray(new JsonObject { ["rule"] = "SHIP-SHA-001", ["message"] = message })));
    }
}
