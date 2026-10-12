using System.Text.Json;
using Aw.Judge.Features.SaveBrief;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge brief save' &lt; input.json: saves the brief and prints nothing; input that isn't a <see cref="BriefInput"/> exits 1 'invalid input: …'.</summary>
internal static class SaveBriefCommand
{
    internal const string Name = "save";

    internal static async Task<int> Run(IServiceProvider services, JsonSerializerOptions json)
    {
        var input = await CommandOutput.ReadInput<BriefInput>(json, CancellationToken.None).ConfigureAwait(false);
        var request = input.Bind(static i => i.ToRequest());
        if (request.IsError)
        {
            return CommandOutput.InvalidInput(request.Error.Message);
        }

        var service = services.GetRequiredService<IService<SaveBriefRequest, SaveBriefResponse>>();
        var result = await service.Handle(request.Value, CancellationToken.None).ConfigureAwait(false);
        return result.IsError ? CommandOutput.Emit(1, "", result.Error.Message) : CommandOutput.Emit(0, "");
    }
}
