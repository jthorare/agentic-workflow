using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.WorktreeSweep;

internal sealed class SweepWorktreesValidator : IValidator<SweepWorktreesRequest>
{
    public Result<Unit> Validate(SweepWorktreesRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Pr is < 1)
        {
            return MetaErrors.Invalid.With("a pull request number from 1 is required");
        }

        if (value.Discarded && value.Path is null)
        {
            return MetaErrors.Invalid.With("--discarded needs --path");
        }

        return value.Path is not null && value.Pr is not null ? MetaErrors.Invalid.With("--path and --pr can't be combined") : Unit.Value;
    }
}
