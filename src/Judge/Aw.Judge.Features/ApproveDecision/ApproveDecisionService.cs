using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ApproveDecision;

// Records that the user cleared a decision (typically an escalated one); approving twice keeps the first time.
internal sealed class ApproveDecisionService(IDecisionLog decisions, TimeProvider clock) : IService<ApproveDecisionRequest, ApproveDecisionResponse>
{
    public async ValueTask<Result<ApproveDecisionResponse>> Handle(ApproveDecisionRequest request, CancellationToken cancellationToken)
    {
        var approved = await decisions.MarkApproved(request.DecisionId, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        try
        {
            var found = await decisions.Find(request.DecisionId, cancellationToken).ConfigureAwait(false);
            return found.IsError ? found.Error : new ApproveDecisionResponse(found.Value);
        }
        catch (InvalidDataException exception)
        {
            // A row the mapper can't read is the row's problem, not a crash (the same answer as 'why'); the change was already made, so say so.
            return JudgeErrors.UnreadableDecision.With($"decision {request.DecisionId.Value} was approved, but it can't be read: {exception.Message.ReplaceLineEndings(" ")}");
        }
    }
}
