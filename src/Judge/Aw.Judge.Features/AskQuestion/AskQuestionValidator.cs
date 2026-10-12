using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Evaluation;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.AskQuestion;

internal sealed class AskQuestionValidator : IValidator<AskQuestionRequest>
{
    public Result<Unit> Validate(AskQuestionRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return QuestionCatalog.Names.Contains(value.Question)
            ? Unit.Value
            : JudgeErrors.Invalid.With($"unknown question: {value.Question}");
    }
}
