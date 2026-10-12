using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetBriefAgentId;

internal sealed class SetBriefAgentIdService(IBriefStore briefs) : IService<SetBriefAgentIdRequest, SetBriefAgentIdResponse>
{
    public async ValueTask<Result<SetBriefAgentIdResponse>> Handle(SetBriefAgentIdRequest request, CancellationToken cancellationToken)
    {
        var mapped = await briefs.SetAgentId(request.ToolUseId, request.AgentId, cancellationToken).ConfigureAwait(false);
        return mapped.IsError ? mapped.Error : new SetBriefAgentIdResponse();
    }
}
