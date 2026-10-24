using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetPromptSort;

internal sealed class SetPromptSortValidator : IValidator<SetPromptSortRequest>
{
    public Result<Unit> Validate(SetPromptSortRequest value) => Unit.Value;
}
