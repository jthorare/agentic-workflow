using Aw.Kernel.Json;
using Aw.Kernel.Telemetry;
using Aw.Sindri.Core.Registration;
using Aw.Sindri.Features;
using Aw.Sindri.Host.Registration;
using Aw.Sindri.Infrastructure.Registration;

namespace Aw.Sindri.Host;

/// <summary>The one composition root, shared by Program.cs and the DI validation test: only the projects' Add…() calls, the clock and the JSON options.</summary>
internal static class Composition
{
    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .AddSingleton(AwJson.CreateOptions(SindriJsonContext.Default))
            .AddKernelTelemetry(TelemetryOptions.FromEnvironment("sindri", environment, clock))   // JSON lines on stderr (AW_LOG_LEVEL); exports off
            .AddSindriCore()
            .AddSindriFeatures()
            .AddSindriInfrastructure(environment)
            .AddSindriHost();
}
