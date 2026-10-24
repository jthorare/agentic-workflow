using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.CheckTraits;

internal sealed class CheckTraitsValidator : IValidator<CheckTraitsRequest>
{
    public Result<Unit> Validate(CheckTraitsRequest value) => Unit.Value;
}
