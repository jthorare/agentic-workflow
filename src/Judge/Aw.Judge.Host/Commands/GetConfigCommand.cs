using System.Text.Json;
using Aw.Judge.Features.GetConfig;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge config get': the effective config as JSON, exit 0; an invalid config.json prints the defaults and logs one warning.</summary>
internal static class GetConfigCommand
{
    internal const string Name = "get";

    internal static async Task<int> Run(IServiceProvider services, JsonSerializerOptions json)
    {
        var service = services.GetRequiredService<IService<GetConfigRequest, GetConfigResponse>>();
        var result = await service.Handle(new GetConfigRequest(), CancellationToken.None).ConfigureAwait(false);
        if (result.IsError)
        {
            return CommandOutput.Emit(1, "", result.Error.Message);
        }

        if (result.Value.Warning is { } warning)
        {
            JudgeLog.UsingDefaultConfig(services.GetRequiredService<ILoggerFactory>().CreateLogger(JudgeLog.Category), warning);
        }

        return CommandOutput.Emit(0, CommandOutput.Json(result.Value.Config, json));
    }
}
