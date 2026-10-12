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

        var found = await decisions.Find(request.DecisionId, cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : new UndoDecisionResponse(found.Value);
    }
}
