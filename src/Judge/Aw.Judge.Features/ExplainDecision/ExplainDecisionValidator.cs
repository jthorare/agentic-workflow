using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ExplainDecision;

internal sealed class ExplainDecisionValidator : IValidator<ExplainDecisionRequest>
{
    public Result<Unit> Validate(ExplainDecisionRequest value) => Unit.Value;
}
