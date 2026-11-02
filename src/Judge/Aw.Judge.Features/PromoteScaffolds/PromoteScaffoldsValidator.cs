using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.PromoteScaffolds;

internal sealed class PromoteScaffoldsValidator : IValidator<PromoteScaffoldsRequest>
{
    public Result<Unit> Validate(PromoteScaffoldsRequest value) => Unit.Value;
}
