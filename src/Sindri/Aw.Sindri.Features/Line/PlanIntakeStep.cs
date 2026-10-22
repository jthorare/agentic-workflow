using System.Text.Json.Nodes;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// 'step.plan.intake', on the block runtime (line spec §6.1): verifies the frozen scope packet and the candidate design packet by recomputing their hashes from their directories (SND-LINE-001
/// on a mismatch, or when the design packet's index does not name the scope packet as its parent) and writes the eight seeds ('fact:anchors' the scope-map keys of the surfaces the design covers); 'fact:design' is the design the packet's files make
/// (<see cref="DesignPacket.FromFiles"/>, with the packet's hash and directory under 'packet'). The port has no verify, so a freeze record is not looked up.
/// </summary>
/// <param name="packets">aw-packets.</param>
/// <param name="files">The packets' files.</param>
internal sealed class PlanIntakeStep(IPackets packets, IPacketFiles files) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.plan.intake");

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

        var design = await LoadedDesign.Load(packets, files, StepFacts.Read(context, "fact:designPacket"), (LineJson.Str(scope.Value, "hash"), "designPacket"), cancellationToken).ConfigureAwait(false);
        return design.IsError
            ? BlockStepResult.Failed($"{Id.Value}: {design.Error.Message}")
            : StepFacts.Done(
                "loaded",
                ("fact:design", design.Value),
                ("fact:plan", new JsonObject()),
                ("fact:findings", new JsonArray()),
                ("fact:questions", new JsonArray()),
                ("fact:answers", new JsonArray()),
                ("fact:answerLog", new JsonArray()),
                ("fact:holdoutSets", new JsonArray()),
                ("fact:anchors", CoveredAnchors(scope.Value, design.Value)));
    }

    /// <summary>The scope map's keys of the surfaces the design's sections cover, in map order: the array 'input.context.code' reads from 'fact:anchors'. Empty when none is covered.</summary>
    private static JsonArray CoveredAnchors(JsonObject scope, JsonObject design)
    {
        var covered = LineJson.Items(design, "sections").SelectMany(static s => LineJson.Strings(s, "covers")).ToHashSet(StringComparer.Ordinal);
        var keys = LineJson.Items(LineJson.Obj(scope, "scopeMap"), "surfaces")
            .Where(s => LineJson.Str(s, "id") is { } id && covered.Contains(id))
            .Select(static s => LineJson.Str(s, "key"))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal);
        return LineJson.ToArray(keys);
    }
}
