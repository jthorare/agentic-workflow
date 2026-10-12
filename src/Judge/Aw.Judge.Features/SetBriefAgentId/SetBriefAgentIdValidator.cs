using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetBriefAgentId;

internal sealed class SetBriefAgentIdValidator : IValidator<SetBriefAgentIdRequest>
{
    public Result<Unit> Validate(SetBriefAgentIdRequest value) => Unit.Value;
}
