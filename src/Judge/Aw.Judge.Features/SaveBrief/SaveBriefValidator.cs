using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SaveBrief;

internal sealed class SaveBriefValidator : IValidator<SaveBriefRequest>
{
    public Result<Unit> Validate(SaveBriefRequest value) => Unit.Value;
}
