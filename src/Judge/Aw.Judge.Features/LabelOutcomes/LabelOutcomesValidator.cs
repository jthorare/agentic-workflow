using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.LabelOutcomes;

internal sealed class LabelOutcomesValidator : IValidator<LabelOutcomesRequest>
{
    public Result<Unit> Validate(LabelOutcomesRequest value) => Unit.Value;
}
