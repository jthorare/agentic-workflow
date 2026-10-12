using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ApproveDecision;

internal sealed class ApproveDecisionValidator : IValidator<ApproveDecisionRequest>
{
    public Result<Unit> Validate(ApproveDecisionRequest value) => Unit.Value;
}
