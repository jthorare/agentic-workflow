using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Ship;

/// <summary>What the pack reads from the packets it hands off, parsed, with the head it is packed on.</summary>
/// <param name="Inputs">The Step's inputs.</param>
/// <param name="Head">The phase worktree's head: what the draft is packed on.</param>
/// <param name="PacketId">The phase packet's id.</param>
/// <param name="Repo">The phase packet's repo.</param>
/// <param name="BaseSha">The phase packet's base commit.</param>
/// <param name="Phase">phase.json.</param>
/// <param name="ProofAcceptanceText">The proof pack's acceptance.json as written (the hand-off's acceptance extends its hash).</param>
/// <param name="ProofAcceptance">The same, parsed.</param>
/// <param name="ProofHoldout">The proof pack's holdout.json.</param>
/// <param name="NotProven">The proof pack's not-proven.md.</param>
/// <param name="Review">The review result's review.json.</param>
internal sealed record HandoffSources(ShipPackInputs Inputs, string Head, string PacketId, string Repo, string BaseSha, JsonNode Phase, string ProofAcceptanceText, JsonNode ProofAcceptance, JsonNode ProofHoldout, string NotProven, JsonNode Review)
{
    /// <summary>The phase's goal.</summary>
    internal string Goal => StepFacts.Field(Phase, "goal") ?? string.Empty;
}
