using Aw.Judge.Core.Ids;
using Aw.Judge.Features.MapBriefByDispatch;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge brief map-by-dispatch &lt;sid&gt; &lt;pid&gt; &lt;type&gt;': the oldest unmapped brief's tool use id for that dispatch, or nothing; exit 0 (hook contract).</summary>
internal static class MapBriefByDispatchCommand
{
    internal const string Name = "map-by-dispatch";

    internal static async Task<int> Run(string sessionId, string promptId, string subagentType, IServiceProvider services)
    {
        if (!SessionId.TryFrom(sessionId, out var session) || !PromptId.TryFrom(promptId, out var prompt))
        {
            return CommandOutput.Emit(0, "");   // past an id's cap: no brief can match
        }

        var service = services.GetRequiredService<IService<MapBriefByDispatchRequest, MapBriefByDispatchResponse>>();
        var result = await service.Handle(new MapBriefByDispatchRequest(session, prompt, subagentType), CancellationToken.None).ConfigureAwait(false);
        return result.IsError
            ? CommandOutput.Emit(1, "", result.Error.Message)
            : CommandOutput.Emit(0, result.Value.ToolUseId?.Value ?? "");
    }
}
