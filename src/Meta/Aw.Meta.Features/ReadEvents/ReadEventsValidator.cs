using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.ReadEvents;

internal sealed class ReadEventsValidator : IValidator<ReadEventsRequest>
{
    public Result<Unit> Validate(ReadEventsRequest value) => Unit.Value;
}
