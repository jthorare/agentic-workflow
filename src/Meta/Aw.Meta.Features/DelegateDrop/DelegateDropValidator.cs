using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.DelegateDrop;

internal sealed class DelegateDropValidator : IValidator<DelegateDropRequest>
{
    public Result<Unit> Validate(DelegateDropRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Issue < 1 ? MetaErrors.Invalid.With("--drop is an issue number, 1 or more") : Unit.Value;
    }
}
