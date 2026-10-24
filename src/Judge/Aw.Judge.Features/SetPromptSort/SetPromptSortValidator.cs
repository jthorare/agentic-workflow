using Aw.Judge.Core.Errors;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetPromptSort;

internal sealed class SetPromptSortValidator : IValidator<SetPromptSortRequest>
{
    public Result<Unit> Validate(SetPromptSortRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return (value.Enabled is null) == (value.Scaffold is null)
            ? JudgeErrors.Invalid.With("set exactly one of enabled and a scaffold switch")
            : Unit.Value;
    }
}
