using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.GetHealth;

internal sealed class GetHealthValidator : IValidator<GetHealthRequest>
{
    public Result<Unit> Validate(GetHealthRequest value) => Unit.Value;
}
