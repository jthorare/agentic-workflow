using System.Text.Json.Nodes;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Review;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Review;

/// <summary>
/// 'step.review.record' on the block runtime (line spec §7.4, DTC4): writes the review result packet (<see cref="ReviewPack"/>) to the instance's run area ('review-pack/') and machine-freezes it through aw-packets
/// (kind review): 'recorded' for a clean review, 'recordedWrong' for a behaviour-wrong one, each with <c language="text">review = {hash, dir, head}</c>. The Step fails (an unrouted failure parks the instance for the owner)
/// when the review holds no verdict a result is recorded under or says clean with blocking or issue findings left, when aw-packets refuses the packet Sindri assembled (its failed checks, PKT codes included, are the message,
/// and nothing is frozen), when a run area cannot be written or a tool cannot run, or when the address, repo, checkout or the phase packet is missing. A relative
/// <c language="text">phasePacket.dir</c> is read under the repo's checkout.
/// </summary>
/// <param name="files">Reads the phase packet's files.</param>
/// <param name="area">The instance's run area, where the packet is written.</param>
/// <param name="packets">Freezes the packet.</param>
/// <param name="checkouts">Where the repo is checked out (the packet's base commit is read from it).</param>
internal sealed class ReviewRecordStep(IBriefFiles files, IRunArea area, IPackets packets, IRepoCheckouts checkouts) : IBlockStep, IBlockStepId, IRepoStep
{
    private const string PackName = "review-pack";

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.review.record");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (ReviewRecordInputs.Of(context) is not { } inputs)
        {
            return BlockStepResult.Failed("step.review.record needs the instance's address and repo, fact:phasePacket (with its hash and dir), fact:change (with its head) and fact:review");
        }

        if (ReviewPack.Problems(inputs.Facts) is { Count: > 0 } problems)
        {
            return BlockStepResult.Failed($"step.review.record can't record the review: {string.Join("; ", problems)}");
        }

        var checkout = await checkouts.Of(inputs.Repo, cancellationToken).ConfigureAwait(false);
        if (checkout.IsError)
        {
            return BlockStepResult.Failed($"step.review.record has no checkout of {inputs.Repo.Value}: {checkout.Error.Message}");
        }

        var dir = Path.IsPathRooted(inputs.Dir) ? inputs.Dir : Path.Combine(checkout.Value, inputs.Dir);
        var (phase, failure) = await ReviewPhasePacket.Load(files, dir, cancellationToken).ConfigureAwait(false);
        if (phase is null)
        {
            return BlockStepResult.Failed($"step.review.record can't load the phase packet {dir}: {failure}");
        }

        var origin = new ReviewPackOrigin(phase.Id + "-review", phase.Repo, phase.BaseSha, inputs.PacketHash, ProofPack.Sha256(phase.AcceptanceText));
        var outcome = string.Equals(ReviewPack.Verdict(inputs.Facts.Review), "clean", StringComparison.Ordinal) ? "recorded" : "recordedWrong";
        return await Freeze(inputs, ReviewPack.Files(inputs.Facts, origin), checkout.Value, outcome, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<BlockStepResult> Freeze(ReviewRecordInputs inputs, IReadOnlyDictionary<string, string> pack, string checkout, string outcome, CancellationToken cancellationToken)
    {
        var address = inputs.Address;
        foreach (var (name, text) in pack)
        {
            var written = await area.Write(address, $"{PackName}/{name}", text, append: false, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return BlockStepResult.Failed($"step.review.record can't write the review packet: {written.Error.Message}");
            }
        }

        var dir = area.PathFor(address, PackName);
        if (dir.IsError)
        {
            return BlockStepResult.Failed($"step.review.record can't place the review packet: {dir.Error.Message}");
        }

        var frozen = await packets.Freeze(new PacketRef(PacketKind.Review, dir.Value, Repo: checkout), PacketFreezer.Machine, cancellationToken).ConfigureAwait(false);
        if (frozen.IsError)
        {
            return BlockStepResult.Failed($"step.review.record can't freeze the review packet with aw-packets: {frozen.Error.Message}");
        }

        return frozen.Value is { Frozen: true, Hash: { } hash }
            ? StepFacts.Done(outcome, ("fact:review", new JsonObject { ["hash"] = hash, ["dir"] = dir.Value, ["head"] = inputs.Facts.Head }))
            : BlockStepResult.Failed($"step.review.record: aw-packets refused the review packet Sindri assembled, which nothing can fix by retrying: {string.Join("; ", frozen.Value.Failures.Select(static f => $"{f.Rule} {f.Subject}: {f.Message}"))}");
    }
}
