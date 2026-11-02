using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ExplainPromptSort;

internal sealed class ExplainPromptSortValidator : IValidator<ExplainPromptSortRequest>
{
    public Result<Unit> Validate(ExplainPromptSortRequest value) => Unit.Value;
}
