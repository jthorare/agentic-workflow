using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.freeze' on the block runtime (scoping spec §3 'Freeze'): re-runs the evidence checks over the facts, hashes the run area's <c language="text">scope-packet/</c>
/// again through aw-packets and compares it with <c language="text">fact:scopePacket</c>, then freezes it as the owner's approval (kind scoping). 'frozen' records
/// <c language="text">fact:scopePacket = {hash, dir}</c> and leaves the directory where it is, since the design station and the scope route read it. A check that no
/// longer holds, a hash that moved or a packet aw-packets refuses is 'mismatch' with findings; a tool that cannot run, or a Step without its address or its recorded hash, fails.
/// The 'scoping.approved' row is the engine's (ScopingApprovals): a Step has no ledger port.
/// </summary>
/// <param name="area">The instance's run area, where the scope packet is kept.</param>
/// <param name="packets">Hashes and freezes the packet.</param>
internal sealed class ScopeFreezeStep(IRunArea area, IPackets packets) : IBlockStep, IBlockStepId
{
    private const string FactPrefix = "fact:";
    private const string PacketName = "scope-packet";

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.freeze");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var recorded = ScopeHash.Of(StepFacts.Read(context, "fact:scopePacket")?.ToJsonString());
        if (context.Address is not { } address || recorded is null)
        {
            return BlockStepResult.Failed($"{Id.Value} needs the instance's address and fact:scopePacket with its hash");
        }

        var checks = DeskBlocks.Run("step.scope.evidence", new ScopeFacts(Texts(context)));
        if (checks.IsError)
        {
            return BlockStepResult.Failed($"{Id.Value} can't re-check the packet: {checks.Error.Message}");
        }

        if (checks.Value.Outcome != "complete")
        {
            return Mismatch(checks.Value.Outputs.TryGetValue("findings", out var findings) && JsonNode.Parse(findings) is JsonArray array ? array : []);
        }

        var dir = area.PathFor(address, PacketName);
        return dir.IsError
            ? BlockStepResult.Failed($"{Id.Value} can't place the scope packet: {dir.Error.Message}")
            : await Freeze(new PacketRef(PacketKind.Scoping, dir.Value), recorded, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<BlockStepResult> Freeze(PacketRef packet, string recorded, CancellationToken cancellationToken)
    {
        var hash = await packets.Hash(packet, cancellationToken).ConfigureAwait(false);
        if (hash.IsError)
        {
            return BlockStepResult.Failed($"{Id.Value} can't hash the scope packet with aw-packets: {hash.Error.Message}");
        }

        if (!string.Equals(hash.Value, recorded, StringComparison.Ordinal))
        {
            return Mismatch([Finding("SCP-FREEZE-001", $"the scope packet hashes to {hash.Value}, not the {recorded} the owner saw", "Run the evidence step again and approve the packet it assembles.")]);
        }

        var frozen = await packets.Freeze(packet, PacketFreezer.Owner, cancellationToken).ConfigureAwait(false);
        if (frozen.IsError)
        {
            return BlockStepResult.Failed($"{Id.Value} can't freeze the scope packet with aw-packets: {frozen.Error.Message}");
        }

        if (frozen.Value is { Frozen: true, Hash: { } frozenHash } && !string.Equals(frozenHash, recorded, StringComparison.Ordinal))
        {
            return Mismatch([Finding("SCP-FREEZE-001", $"aw-packets froze {frozenHash}, not the {recorded} the owner saw: the packet changed while it was frozen", "Run the evidence step again and approve the packet it assembles.")]);
        }

        return frozen.Value is { Frozen: true, Hash: { } sameHash }
            ? StepFacts.Done("frozen", ("fact:scopePacket", new JsonObject { ["hash"] = sameHash, ["dir"] = packet.Directory }))
            : Mismatch(new JsonArray([.. frozen.Value.Failures.Select(static f => Finding(f.Rule, f.Message, f.Fix ?? "Fix the packet and approve it again.", f.Subject))]));
    }

    private static BlockStepResult Mismatch(JsonArray findings) => StepFacts.Done("mismatch", ("fact:findings", findings));

    private static JsonObject Finding(string rule, string message, string fix, string? item = null) =>
        new() { ["rule"] = rule, ["item"] = item, ["message"] = message, ["fix"] = fix, ["severity"] = "fail" };

    private static Dictionary<string, string> Texts(StepContext context) => context.Facts
        .Where(static f => f.Key.Value.StartsWith(FactPrefix, StringComparison.Ordinal))
        .ToDictionary(static f => f.Key.Value[FactPrefix.Length..], static f => f.Value.ValueKind == JsonValueKind.String ? f.Value.GetString() ?? string.Empty : f.Value.GetRawText(), StringComparer.Ordinal);
}
