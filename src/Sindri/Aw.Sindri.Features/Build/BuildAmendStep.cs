using System.Text.Json.Nodes;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Build;

/// <summary>
/// 'step.build.amend' on the block runtime (line spec §7.2, LN-PKT1, LN-PKT5): the owner's grant (the 'allow' paths of the object 'fact:answers') becomes a new child phase packet
/// (<see cref="AmendedPhasePacket"/>) in this instance's run area under packets/&lt;id&gt;, owner-frozen through aw-packets, and the packet in force. The scope grant never edits the
/// frozen packet. The LC-7 escape is a line of the run area's escapes.jsonl: the Step reads no ledger, so the 'line.escape' row is the engine's to write from it. The Step reads
/// 'fact:activePacket' and 'fact:answers' only, so the child goes to the instance's own run area, not the line's.
/// </summary>
/// <param name="packets">Freezes the child packet.</param>
/// <param name="files">Reads the parent packet and writes the child into the run area.</param>
/// <param name="area">Keeps the escape line.</param>
/// <param name="clock">Stamps the amendment.</param>
internal sealed class BuildAmendStep(IPackets packets, IPacketDirectory files, IRunArea area, TimeProvider clock) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.build.amend");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Address is not { } address || ActivePacket.From(StepFacts.Read(context, "fact:activePacket")) is not { } active)
        {
            return BlockStepResult.Failed("step.build.amend needs fact:activePacket ({ hash, dir, repo }) and an instance address");
        }

        if (active.Repo is null)
        {
            return BlockStepResult.Failed("step.build.amend needs the repo of fact:activePacket: a checkout aw-packets can check the packet's base commit in (fix: step.build.prepare sets it from fact:workspace)");
        }

        if (ScopeGrant.From(StepFacts.Read(context, "fact:answers")) is not { } grant)
        {
            return BlockStepResult.Failed("step.build.amend found no 'allow' paths in fact:answers ({ allow: [paths], reason })");
        }

        return await Amend(address, active, grant, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<BlockStepResult> Amend(string address, ActivePacket active, ScopeGrant grant, CancellationToken cancellationToken)
    {
        var parent = await files.Read(active.Dir, cancellationToken).ConfigureAwait(false);
        if (parent.IsError)
        {
            return BlockStepResult.Failed($"step.build.amend can't read the packet in {active.Dir}: {parent.Error.Message}");
        }

        var child = AmendedPhasePacket.From(parent.Value, active.Hash, grant, clock.GetUtcNow());
        if (child.IsError)
        {
            return BlockStepResult.Failed($"step.build.amend: {child.Error.Message}");
        }

        var directory = await files.Create(address, "packets/" + child.Value.Id, child.Value.Files, cancellationToken).ConfigureAwait(false);
        if (directory.IsError)
        {
            return BlockStepResult.Failed($"step.build.amend can't write the child packet: {directory.Error.Message}");
        }

        var freeze = await packets.Freeze(new PacketRef(PacketKind.Phase, directory.Value, active.Repo), PacketFreezer.Owner, cancellationToken).ConfigureAwait(false);
        if (freeze.IsError || !freeze.Value.Frozen || freeze.Value.Hash is null)
        {
            return BlockStepResult.Failed($"step.build.amend can't freeze {child.Value.Id}: {(freeze.IsError ? freeze.Error.Message : PacketFailure.Describe(freeze.Value.Failures))}");
        }

        var escape = new JsonObject { ["from"] = "build", ["station"] = "amend", ["kind"] = "scopeGrant", ["id"] = child.Value.Id, ["parent"] = active.Hash };
        var written = await area.Write(address, "escapes.jsonl", escape.ToJsonString() + "\n", append: true, cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? BlockStepResult.Failed($"step.build.amend can't record the escape: {written.Error.Message}")
            : StepFacts.Done("amended", ("fact:activePacket", (active with { Hash = freeze.Value.Hash, Dir = directory.Value }).ToFact()));
    }
}
