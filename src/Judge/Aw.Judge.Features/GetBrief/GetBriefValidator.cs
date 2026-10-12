using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.GetBrief;

internal sealed class GetBriefValidator : IValidator<GetBriefRequest>
{
    public Result<Unit> Validate(GetBriefRequest value) => Unit.Value;
}
