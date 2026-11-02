using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetLabel;

internal sealed class SetLabelValidator : IValidator<SetLabelRequest>
{
    public Result<Unit> Validate(SetLabelRequest value) => Unit.Value;
}
