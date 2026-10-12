using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ExplainDecision;

internal sealed class ExplainDecisionService(IDecisionLog decisions) : IService<ExplainDecisionRequest, ExplainDecisionResponse>
{
    public async ValueTask<Result<ExplainDecisionResponse>> Handle(ExplainDecisionRequest request, CancellationToken cancellationToken)
    {
        var found = await decisions.Find(request.DecisionId, cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : new ExplainDecisionResponse(found.Value);
    }
}
