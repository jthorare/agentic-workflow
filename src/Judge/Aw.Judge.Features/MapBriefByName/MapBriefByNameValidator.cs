using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.MapBriefByName;

internal sealed class MapBriefByNameValidator : IValidator<MapBriefByNameRequest>
{
    public Result<Unit> Validate(MapBriefByNameRequest value) => Unit.Value;
}
