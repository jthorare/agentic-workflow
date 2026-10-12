using Aw.Judge.Core.Ids;
using Aw.Judge.Features.SetBriefAgentId;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge brief set-agent-id &lt;toolUseId&gt; &lt;agentId&gt;': maps the brief to its agent, prints nothing, exit 0 (hook contract).</summary>
internal static class SetBriefAgentIdCommand
{
    internal const string Name = "set-agent-id";

    internal static async Task<int> Run(string toolUseId, string agentId, IServiceProvider services)
    {
        if (!ToolUseId.TryFrom(toolUseId, out var brief) || !AgentId.TryFrom(agentId, out var agent))
        {
            return CommandOutput.Emit(0, "");   // past an id's cap: no brief can match, as an unknown tool use id changes nothing
        }

        var service = services.GetRequiredService<IService<SetBriefAgentIdRequest, SetBriefAgentIdResponse>>();
        var result = await service.Handle(new SetBriefAgentIdRequest(brief, agent), CancellationToken.None).ConfigureAwait(false);
        return result.IsError ? CommandOutput.Emit(1, "", result.Error.Message) : CommandOutput.Emit(0, "");
    }
}
