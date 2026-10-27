using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ExplainDecision;

internal sealed class ExplainDecisionService(IDecisionLog decisions) : IService<ExplainDecisionRequest, ExplainDecisionResponse>
{
    public async ValueTask<Result<ExplainDecisionResponse>> Handle(ExplainDecisionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var found = await decisions.Find(request.DecisionId, cancellationToken).ConfigureAwait(false);
            return found.IsError ? found.Error : new ExplainDecisionResponse(found.Value);
        }
        catch (InvalidDataException exception)
        {
            // A row the mapper can't read (a column written in a form this judge doesn't accept) is the row's problem, not a crash.
            return JudgeErrors.UnreadableDecision.With($"decision {request.DecisionId.Value} can't be read: {exception.Message.ReplaceLineEndings(" ")}");
        }
    }
}
