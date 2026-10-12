using Aw.Judge.Features.MapBriefByName;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge brief map-by-name &lt;name&gt;': the oldest unmapped brief's tool use id under that name, or nothing; exit 0 (hook contract).</summary>
internal static class MapBriefByNameCommand
{
    internal const string Name = "map-by-name";

    internal static async Task<int> Run(string dispatchName, IServiceProvider services)
    {
        var service = services.GetRequiredService<IService<MapBriefByNameRequest, MapBriefByNameResponse>>();
        var result = await service.Handle(new MapBriefByNameRequest(dispatchName), CancellationToken.None).ConfigureAwait(false);
        return result.IsError
            ? CommandOutput.Emit(1, "", result.Error.Message)
            : CommandOutput.Emit(0, result.Value.ToolUseId?.Value ?? "");
    }
}
