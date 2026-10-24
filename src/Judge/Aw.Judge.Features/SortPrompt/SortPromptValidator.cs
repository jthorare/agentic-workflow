using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SortPrompt;

internal sealed class SortPromptValidator : IValidator<SortPromptRequest>
{
    public Result<Unit> Validate(SortPromptRequest value) => Unit.Value;
}
