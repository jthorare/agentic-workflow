using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.LensLoops;

/// <summary>
/// 'step.review.round', on the block runtime (line spec §5.2, DTC4, LN-PK lite): resolves the review lens pack the approved profile chooses, queues the lenses that apply to the paths the change
/// touches ('when'), and records each other lens as 'notApplicable' in 'fact:lensRounds' with the reason. 'ready'; 'noLens' when none applies (the loop ends stuck for the owner, never a clean review of
/// unreviewed code). A change that names no base and head (the empty change a bug fix passes) has no known paths, so every lens applies.
/// </summary>
/// <param name="packs">Resolves the pack.</param>
/// <param name="changed">Lists the paths base..head touches.</param>
/// <param name="checkouts">Where the repo is checked out.</param>
internal sealed class ReviewRoundStep(ILensPacks packs, IChangedPaths changed, IRepoCheckouts checkouts) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.review.round");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var (rounds, reason) = RoundSeed.Log(context);
        if (rounds is null)
        {
            return BlockStepResult.Failed($"step.review.round: {reason}");
        }

        var pack = await packs.Resolve(LensPackKind.Review, context.Repo, cancellationToken).ConfigureAwait(false);
        if (pack.IsError)
        {
            return BlockStepResult.Failed($"step.review.round can't resolve the review lens pack: {pack.Error.Message}");
        }

        var (paths, failure) = await Paths(context, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return BlockStepResult.Failed($"step.review.round can't list the changed paths: {failure}");
        }

        var selection = LensSelection.Of(pack.Value, surfaceKinds: null, paths);
        return selection.Queued.Count == 0 ? StepFacts.Done("noLens") : RoundSeed.Ready(pack.Value, selection, rounds);
    }

    private async ValueTask<(IReadOnlyList<string>? Value, string? Failure)> Paths(StepContext context, CancellationToken cancellationToken)
    {
        var change = StepFacts.Read(context, "fact:change");
        if (LoopJson.Str(change, "base") is not { Length: > 0 } baseRef || LoopJson.Str(change, "head") is not { Length: > 0 } head || context.Repo is not { } repo)
        {
            return (null, null);
        }

        var checkout = await checkouts.Of(repo, cancellationToken).ConfigureAwait(false);
        if (checkout.IsError)
        {
            return (null, checkout.Error.Message);
        }

        var listed = await changed.Between(checkout.Value, baseRef, head, cancellationToken).ConfigureAwait(false);
        return listed.IsError ? (null, listed.Error.Message) : (listed.Value, null);
    }
}
