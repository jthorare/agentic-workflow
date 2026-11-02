using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.EvaluateSort;

internal sealed class EvaluateSortValidator : IValidator<EvaluateSortRequest>
{
    public Result<Unit> Validate(EvaluateSortRequest value) => Unit.Value;
}
