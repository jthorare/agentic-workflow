using System.Text.Json;
using Aw.Judge.Core.Ids;
using Aw.Judge.Features.GetBrief;
using Aw.Kernel;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge brief get &lt;toolUseId&gt;': the brief as JSON, or exit 1 'no brief for tool_use_id: …' (hook contract).</summary>
internal static class GetBriefCommand
{
    internal const string Name = "get";

    internal static async Task<int> Run(string toolUseId, IServiceProvider services, JsonSerializerOptions json)
    {
        var none = $"no brief for tool_use_id: {toolUseId}";
        if (!ToolUseId.TryFrom(toolUseId, out var id))
        {
            return CommandOutput.Emit(1, "", none);   // past the id's cap: nothing can have been saved under it
        }

        var service = services.GetRequiredService<IService<GetBriefRequest, GetBriefResponse>>();
        var result = await service.Handle(new GetBriefRequest(id), CancellationToken.None).ConfigureAwait(false);
        if (result.IsError)
        {
            return CommandOutput.Emit(1, "", result.Error.Kind == ErrorKind.NotFound ? none : result.Error.Message);
        }

        return CommandOutput.Emit(0, CommandOutput.Json(BriefOutput.From(result.Value.Brief), json));
    }
}
