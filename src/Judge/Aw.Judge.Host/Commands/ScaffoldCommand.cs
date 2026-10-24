using System.Text.Json;
using Aw.Judge.Features.SetPromptSort;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge prompt-sort scaffold &lt;name&gt; on|off': writes the scaffold's switch into config.json and prints the new config. Unit 6.1 builds the write.</summary>
internal static class ScaffoldCommand
{
    internal const string Name = "scaffold";
    internal const string Synopsis = "prompt-sort scaffold <name> on|off";

    internal static async Task<int> Run(IReadOnlyList<string> args, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Count != 2 || args[1] is not ("on" or "off"))
        {
            return CommandOutput.Usage(Synopsis);
        }

        var service = services.GetRequiredService<IService<SetPromptSortRequest, SetPromptSortResponse>>();
        var request = new SetPromptSortRequest(Scaffold: new ScaffoldSwitch(args[0], string.Equals(args[1], "on", StringComparison.Ordinal)));
        var result = await service.Handle(request, CancellationToken.None).ConfigureAwait(false);
        return result.IsError
            ? CommandOutput.Emit(1, "", result.Error.Message)
            : CommandOutput.Emit(0, CommandOutput.Json(result.Value.Config, json));
    }
}
