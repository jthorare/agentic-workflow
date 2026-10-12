using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.MapBriefByDispatch;

internal sealed class MapBriefByDispatchValidator : IValidator<MapBriefByDispatchRequest>
{
    public Result<Unit> Validate(MapBriefByDispatchRequest value) => Unit.Value;
}
