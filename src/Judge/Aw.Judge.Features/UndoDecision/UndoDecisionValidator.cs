using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.UndoDecision;

internal sealed class UndoDecisionValidator : IValidator<UndoDecisionRequest>
{
    public Result<Unit> Validate(UndoDecisionRequest value) => Unit.Value;
}
