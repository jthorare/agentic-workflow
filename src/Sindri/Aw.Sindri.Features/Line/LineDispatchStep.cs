using System.Text.Json.Nodes;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// 'step.line.dispatch' on the block runtime (line spec §8.2, LN-WF1): folds the last <c language="text">fact:phaseEvent</c> and the owner's <c language="text">fact:boardEdit</c> into <c language="text">fact:phaseBoard</c>
/// and reports <c language="text">start</c> (the first ready phase in plan order, <c language="text">fact:nextPhase</c>, already marked running on the board), <c language="text">full</c> (at <c language="text">workflow.line.parallel</c>
/// running phases or <c language="text">workflow.line.maxOpenPrs</c> open PRs, or nothing ready while phases still run), <c language="text">done</c> (every phase merged or skipped) or <c language="text">blocked</c> (a failed phase
/// holds the line for the owner, or nothing can start and nothing runs). The event and the edit are written back empty, so a later run does not fold them twice. Pure apart from the profile read.
/// </summary>
/// <param name="profile">The approved profile, whose <c language="text">workflow.line</c> keys cap the dispatch (defaults 2 and 4).</param>
internal sealed class LineDispatchStep(IApprovedProfile profile) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.line.dispatch");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var board = PhaseBoard.Read(StepFacts.Read(context, "fact:phaseBoard"));
        if (board is null)
        {
            return BlockStepResult.Failed($"{Id.Value} needs fact:phaseBoard, the board step.line.seed writes");
        }

        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return BlockStepResult.Failed($"{Id.Value} can't read the approved profile: {approved.Error.Message}");
        }

        board.Fold(StepFacts.Read(context, "fact:phaseEvent"));
        board.Edit(StepFacts.Read(context, "fact:boardEdit"));
        var settings = approved.Value?.Profile ?? new SindriProfile(1, string.Empty);   // nothing approved: the defaults
        var decision = board.Decide(LineProfile.Parallel(settings), LineProfile.MaxOpenPrs(settings));
        if (decision.Next is { } next)
        {
            PhaseBoard.Start(next);
            return StepFacts.Done("start", ("fact:phaseBoard", board.ToJson()), ("fact:nextPhase", next.ToJson()), ("fact:phaseEvent", new JsonObject()), ("fact:boardEdit", new JsonObject()));
        }

        return StepFacts.Done(decision.Outcome, ("fact:phaseBoard", board.ToJson()), ("fact:phaseEvent", new JsonObject()), ("fact:boardEdit", new JsonObject()));
    }
}
