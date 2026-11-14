using System.Text.Json;
using Aw.Judge.Features.GetHealth;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>The 'health' command: {"status","failures24h"}, exit 0 (hook contract: judge-health.sh reads both).</summary>
internal static class GetHealthCommand
{
    internal const string Name = "health";

    private static HealthOutput Output(GetHealthResponse health) =>
        new(Frozen.Text(health.Status), health.Failures24h, health.LastFailure is { } last ? new HealthFailureOutput(last.Provider, last.ReasonCode, last.Ts) : null);

    internal static async Task<int> Run(IServiceProvider services, JsonSerializerOptions json)
    {
        var service = services.GetRequiredService<IService<GetHealthRequest, GetHealthResponse>>();
        var result = await service.Handle(new GetHealthRequest(), CancellationToken.None).ConfigureAwait(false);
        return result.IsError
            ? CommandOutput.Emit(1, "", result.Error.Message)
            : CommandOutput.Emit(0, CommandOutput.Json(Output(result.Value), json));
    }
}
