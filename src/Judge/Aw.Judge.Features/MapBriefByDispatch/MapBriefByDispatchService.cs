using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.MapBriefByDispatch;

internal sealed class MapBriefByDispatchService(IBriefStore briefs) : IService<MapBriefByDispatchRequest, MapBriefByDispatchResponse>
{
    public async ValueTask<Result<MapBriefByDispatchResponse>> Handle(MapBriefByDispatchRequest request, CancellationToken cancellationToken)
    {
        var found = await briefs.FirstUnmappedByDispatch(request.SessionId, request.PromptId, request.SubagentType, cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : new MapBriefByDispatchResponse(found.Value?.ToolUseId);
    }
}
