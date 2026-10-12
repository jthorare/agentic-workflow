using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.MapBriefByName;

internal sealed class MapBriefByNameService(IBriefStore briefs) : IService<MapBriefByNameRequest, MapBriefByNameResponse>
{
    public async ValueTask<Result<MapBriefByNameResponse>> Handle(MapBriefByNameRequest request, CancellationToken cancellationToken)
    {
        var found = await briefs.FirstUnmappedByDispatchName(request.DispatchName, cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : new MapBriefByNameResponse(found.Value?.ToolUseId);
    }
}
