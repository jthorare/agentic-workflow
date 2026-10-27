using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.EvaluateQuestion;

internal sealed class EvaluateQuestionValidator : IValidator<EvaluateQuestionRequest>
{
    public Result<Unit> Validate(EvaluateQuestionRequest value) => Unit.Value;
}
