using Aw.Judge.Core.Providers;
using Aw.Judge.Features.CheckImages;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// 'judge check images': runs Jev and the agent CLI on the embedded labeled screenshots and says whether Jev should judge screenshots
/// (docs/csharp/judge.md). Exit 0 when the bar holds, 1 when it is missed, there is no Jev key or no case is embedded, 64 for usage.
/// </summary>
internal static class CheckCommand
{
    internal const string Name = "check";
    internal const string Synopsis = "check images";

    internal static async Task<int> Run(IReadOnlyList<string> args, IReadOnlyList<ProviderName>? allowed, IServiceProvider services)
    {
        if (args.Count != 1 || !string.Equals(args[0], "images", StringComparison.Ordinal))
        {
            return CommandOutput.Usage(Synopsis, "the only check is images");
        }

        var service = services.GetRequiredService<IService<CheckImagesRequest, CheckImagesResponse>>();
        var result = await service.Handle(new CheckImagesRequest(allowed, services.GetRequiredService<JevImagesVariable>().Value), CancellationToken.None).ConfigureAwait(false);
        return result.IsError
            ? CommandOutput.Emit(1, "", result.Error.Message)
            : CommandOutput.Emit(result.Value.Held ? 0 : 1, string.Join('\n', result.Value.Lines));
    }
}
