using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Gate;

/// <summary>
/// 'gate.revision-bound' (PR babysitter spec §10.1, IDEA-009): passes only while the snapshot 'at' (the argument, else
/// 'fact:prState') is green and its 'mergeBlockedBy' is empty. 'heldComments' blocks only when the snapshot says
/// 'heldBlocksMerge' (the profile's 'workflow.pr.heldBlocksMerge'). Facts only: the head is re-read by the merge
/// effect's expected-head check, not here.
/// </summary>
internal sealed class RevisionBoundGate : IGate, IDescribedBlock
{
    private const string Held = "heldComments";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrGates.RevisionBound;

    /// <inheritdoc/>
    public ValueTask<GateResult> Evaluate(GateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var reasons = ReasonsOf(SnapshotOf(context));
        return ValueTask.FromResult(new GateResult(reasons.Count == 0 ? GateVerdict.Pass : GateVerdict.Fail, Score: null, reasons));
    }

    private static JsonElement? SnapshotOf(GateContext context)
    {
        if (context.Arguments.TryGetValue("at", out var at) && at.ValueKind == JsonValueKind.Object)
        {
            return at;
        }

        return context.Facts.TryGetValue(FactKey.From("fact:prState"), out var fact) && fact.ValueKind == JsonValueKind.Object ? fact : null;
    }

    private static List<string> ReasonsOf(JsonElement? snapshot)
    {
        var reasons = new List<string>();
        if (snapshot is not { } at)
        {
            reasons.Add("there is no pull request snapshot to merge from; check the pull request first");
            return reasons;
        }

        if (!at.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.String || !string.Equals(state.GetString(), "green", StringComparison.Ordinal))
        {
            reasons.Add($"the checks are not green on the head (state {(state.ValueKind == JsonValueKind.String ? state.GetString() : "unknown")}); check the pull request again");
        }

        var heldBlocks = at.TryGetProperty("heldBlocksMerge", out var held) && held.ValueKind == JsonValueKind.True;
        if (at.TryGetProperty("mergeBlockedBy", out var blockers) && blockers.ValueKind == JsonValueKind.Array)
        {
            reasons.AddRange(blockers.EnumerateArray()
                .Select(static b => b.ValueKind == JsonValueKind.String ? b.GetString() : null)
                .OfType<string>()
                .Where(b => heldBlocks || !string.Equals(b, Held, StringComparison.Ordinal))
                .Select(static b => $"the merge is blocked by {b}"));
        }

        return reasons;
    }
}
