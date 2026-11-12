using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Events;

namespace Aw.Meta.Features.ReadEvents;

internal sealed class ReadEventsValidator : IValidator<ReadEventsRequest>
{
    public Result<Unit> Validate(ReadEventsRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Kind is not null && !MetaEventRules.IsKind(value.Kind))
        {
            return MetaErrors.EventInvalid.With("the kind '" + value.Kind + "' is not dotted lower-case (such as obligation.opened)");
        }

        if (value.After < 0)
        {
            return MetaErrors.Invalid.With("--after must be 0 or more");
        }

        return value.Max < 1 ? MetaErrors.Invalid.With("--max must be 1 or more") : Unit.Value;
    }
}
