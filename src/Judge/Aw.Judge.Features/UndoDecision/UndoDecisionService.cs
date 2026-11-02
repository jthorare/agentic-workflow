using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.UndoDecision;

internal sealed class UndoDecisionService(IDecisionLog decisions, TimeProvider clock) : IService<UndoDecisionRequest, UndoDecisionResponse>
{
    public async ValueTask<Result<UndoDecisionResponse>> Handle(UndoDecisionRequest request, CancellationToken cancellationToken)
    {
        var undone = await decisions.MarkUndone(request.DecisionId, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (undone.IsError)
        {
            return undone.Error;
        }

        try
        {
            var found = await decisions.Find(request.DecisionId, cancellationToken).ConfigureAwait(false);
            return found.IsError ? found.Error : new UndoDecisionResponse(found.Value);
        }
        catch (InvalidDataException exception)
        {
            // A row the mapper can't read is the row's problem, not a crash (the same answer as 'why'); the change was already made, so say so.
            return JudgeErrors.UnreadableDecision.With($"decision {request.DecisionId.Value} was undone, but it can't be read: {exception.Message.ReplaceLineEndings(" ")}");
        }
    }
}
