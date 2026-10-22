using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// 'step.design.intake', on the block runtime (line spec §4.1): verifies the frozen scope packet (its directory hashes to the hash the fact names: SND-LINE-001 otherwise), loads it
/// into 'fact:scope' (<see cref="LineScope"/>) and writes the eight round-1 seeds. 'fresh', or 'revise' when 'fact:designRevision' holds decisions or notes (the first pass holds
/// text, which is not a revision); a revision seeds 'fact:answers' with it, for the apply Step, and loads the previous design packet ('fact:priorDesignPacket', verified as the
/// scope packet is, SND-LINE-001) into 'fact:design', so apply edits that design; a revision with no previous packet fails, never seeding an empty design. The style and lens packs are not
/// resolved here: no pack resolver exists until the pack phase (LN-PK), so 'fact:packs' starts empty and SND-PACK-001/-002 are not checked.
/// </summary>
/// <param name="packets">aw-packets.</param>
/// <param name="files">The scope packet's files.</param>
internal sealed class DesignIntakeStep(IPackets packets, IPacketFiles files) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.design.intake");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var scope = await LineScope.Load(packets, files, StepFacts.Read(context, "fact:scopePacket"), cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return BlockStepResult.Failed($"{Id.Value}: {scope.Error.Message}");
        }

        var revision = StepFacts.Read(context, "fact:designRevision");
        var revise = IsRevision(revision);
        var design = revise ? await PriorDesign(context, scope.Value, cancellationToken).ConfigureAwait(false) : new JsonObject();
        return design.IsError
            ? BlockStepResult.Failed($"{Id.Value}: {design.Error.Message}")
            : StepFacts.Done(
                revise ? "revise" : "fresh",
                ("fact:scope", scope.Value),
                ("fact:packs", new JsonObject()),
                ("fact:design", design.Value),
                ("fact:findings", new JsonArray()),
                ("fact:lensRounds", new JsonArray()),
                ("fact:changedSections", new JsonArray()),
                ("fact:answers", revise && revision is not null ? new JsonArray(revision.DeepClone()) : []),
                ("fact:accepted", new JsonArray()));
    }

    private async ValueTask<Result<JsonObject>> PriorDesign(StepContext context, JsonNode scope, CancellationToken cancellationToken)
    {
        var prior = StepFacts.Read(context, "fact:priorDesignPacket");
        return prior is JsonObject
            ? await LoadedDesign.Load(packets, files, prior, (LineJson.Str(scope, "hash"), "priorDesignPacket"), cancellationToken).ConfigureAwait(false)
            : SindriErrors.InvalidInput.With("a revision has no previous design packet in fact:priorDesignPacket to edit (fix: start the design station with the previous design packet; a revision edits the previous design and never starts from an empty one)");
    }

    // A revision from stop 2 is an object with decisions or notes; the first pass passes a sentence, which is no revision.
    private static bool IsRevision(JsonNode? revision) =>
        LineJson.Items(revision, "decisions").Any() || LineJson.Items(revision, "notes").Any();
}
