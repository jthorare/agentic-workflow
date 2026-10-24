using System.Text.Json.Nodes;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Ship;

/// <summary>What the pack Step needs from its context: where it runs, the three packets it hands off, the phase worktree and the line it belongs to.</summary>
/// <param name="Address">The instance's address; its root is the line's slug in the holdout store.</param>
/// <param name="Repo">The instance's repo.</param>
/// <param name="PacketDir">The phase packet's directory.</param>
/// <param name="PacketHash">The phase packet's hash.</param>
/// <param name="Stack">The branch the phase is stacked on, null for a phase on the default branch.</param>
/// <param name="ProofDir">The proof pack's directory.</param>
/// <param name="ProofHash">The proof pack's hash.</param>
/// <param name="ReviewDir">The review result's directory.</param>
/// <param name="ReviewHash">The review result's hash.</param>
/// <param name="Workspace">The phase worktree.</param>
/// <param name="Commands">The commands the local GREEN gate ran, from fact:green.</param>
/// <param name="Line">The line's name, phase and attempt from fact:lineContext; null outside a line (an empty context).</param>
internal sealed record ShipPackInputs(string Address, RepoName Repo, string PacketDir, string PacketHash, string? Stack, string ProofDir, string ProofHash, string ReviewDir, string ReviewHash, string Workspace, IReadOnlyList<string> Commands, LineRef? Line)
{
    /// <summary>Reads them; null when the context lacks one.</summary>
    /// <param name="context">The Step's context.</param>
    /// <returns>The inputs.</returns>
    internal static ShipPackInputs? Of(StepContext context)
    {
        var packet = StepFacts.Read(context, "fact:phasePacket");
        var proof = StepFacts.Read(context, "fact:proofPack");
        var review = StepFacts.Read(context, "fact:review");
        return context.Address is { } address && context.Repo is { } repo
            && StepFacts.Field(packet, "dir") is { } dir && StepFacts.Field(packet, "hash") is { } hash
            && StepFacts.Field(proof, "dir") is { } proofDir && StepFacts.Field(proof, "hash") is { } proofHash
            && StepFacts.Field(review, "dir") is { } reviewDir && StepFacts.Field(review, "hash") is { } reviewHash
            && StepFacts.Text(context, "fact:workspace") is { } workspace
                ? new ShipPackInputs(address, repo, dir, hash, StackOf(packet), proofDir, proofHash, reviewDir, reviewHash, workspace, CommandsOf(StepFacts.Read(context, "fact:green")), LineRef.Of(StepFacts.Read(context, "fact:lineContext")))
                : null;
    }

    // The line's start effect writes base as { rule, ref }, the parent's head branch as 'ref' for a dependency-head phase and an empty 'ref' for a phase on the default branch.
    private static string? StackOf(JsonNode? packet) =>
        string.Equals(StepFacts.Field(packet?["base"], "rule"), "dependency-head", StringComparison.Ordinal) ? StepFacts.Field(packet?["base"], "ref") : null;

    private static string[] CommandsOf(JsonNode? green) => green?["commands"] is JsonArray commands
        ? [.. commands.OfType<JsonValue>().Select(static c => c.TryGetValue<string>(out var text) ? text : null).OfType<string>()]
        : [];
}
