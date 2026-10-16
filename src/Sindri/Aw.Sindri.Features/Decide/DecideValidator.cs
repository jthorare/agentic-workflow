using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.Decide;

/// <summary>The shape of a decision; the rules (DecisionRules.Decide, SND-DEC-001) arrive with the implementation.</summary>
internal sealed class DecideValidator : IValidator<DecideRequest>
{
    public Result<Unit> Validate(DecideRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Unit.Value;
    }
}
