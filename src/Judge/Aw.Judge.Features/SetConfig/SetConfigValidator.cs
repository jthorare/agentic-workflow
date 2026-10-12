using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Evaluation;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetConfig;

internal sealed class SetConfigValidator : IValidator<SetConfigRequest>
{
    public Result<Unit> Validate(SetConfigRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Question.Length == 0)
        {
            return JudgeErrors.Invalid.With("the question name is empty");
        }

        if (!QuestionCatalog.Names.Contains(value.Question))
        {
            // A typo would write an entry nothing reads, and the user would think the question changed.
            return JudgeErrors.Invalid.With($"unknown question: {value.Question} (known: {string.Join(", ", QuestionCatalog.Names.Order(StringComparer.Ordinal))})");
        }

        return (value.Enabled is null) == (value.Threshold is null)
            ? JudgeErrors.Invalid.With("set exactly one of enabled and threshold")
            : Unit.Value;
    }
}
