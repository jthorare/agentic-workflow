using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.GetBrief;

internal sealed class GetBriefService(IBriefStore briefs) : IService<GetBriefRequest, GetBriefResponse>
{
    public async ValueTask<Result<GetBriefResponse>> Handle(GetBriefRequest request, CancellationToken cancellationToken)
    {
        var found = await briefs.ByToolUseId(request.ToolUseId, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        return found.Value is { } brief
            ? new GetBriefResponse(brief)
            : JudgeErrors.NoBrief.With($"no brief for tool_use_id: {request.ToolUseId.Value}");
    }
}
