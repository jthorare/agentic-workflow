using Aw.Judge.Core;
using Aw.Judge.Features;
using Aw.Judge.Host.Commands;
using Aw.Judge.Infrastructure;
using Aw.Kernel.Json;
using Aw.Kernel.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host;

/// <summary>The one composition root, shared by Program.cs and the DI validation test.</summary>
internal static class Composition
{
    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .AddSingleton(AwJson.CreateOptions(JudgeJsonContext.Default))   // the Kernel JSON profile: the config file and Core's stored-input JSON
            .AddKernelTelemetry(TelemetryOptions.FromEnvironment("judge", environment, clock))   // JSON lines on stderr (AW_LOG_LEVEL); exports off
            .AddSingleton(CallingSession.From(environment("AW_SESSION_ID")))
            .AddSingleton(new ProvidersVariable(environment("AW_JUDGE_PROVIDERS")))
            .AddJudgeCore()
            .AddJudgeFeatures()
            .AddJudgeInfrastructure(environment);
}
