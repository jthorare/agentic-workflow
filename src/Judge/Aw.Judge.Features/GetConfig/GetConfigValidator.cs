using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.GetConfig;

internal sealed class GetConfigValidator : IValidator<GetConfigRequest>
{
    public Result<Unit> Validate(GetConfigRequest value) => Unit.Value;
}
