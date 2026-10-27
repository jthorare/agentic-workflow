using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.LabelSortOutcomes;

internal sealed class LabelSortOutcomesValidator : IValidator<LabelSortOutcomesRequest>
{
    public Result<Unit> Validate(LabelSortOutcomesRequest value) => Unit.Value;
}
