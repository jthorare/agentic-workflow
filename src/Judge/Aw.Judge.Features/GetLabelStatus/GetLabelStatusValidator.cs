using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.GetLabelStatus;

internal sealed class GetLabelStatusValidator : IValidator<GetLabelStatusRequest>
{
    public Result<Unit> Validate(GetLabelStatusRequest value) => Unit.Value;
}
