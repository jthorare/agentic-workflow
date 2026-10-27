using Aw.Judge.Core.Errors;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.Adjudicate;

internal sealed class AdjudicateValidator : IValidator<AdjudicateRequest>
{
    public Result<Unit> Validate(AdjudicateRequest value) =>
        value.Limit < 1 ? JudgeErrors.Invalid.With("--limit must be at least 1") : Unit.Value;
}
