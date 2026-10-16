using Aw.Evidence.Core.Registration;
using Aw.Evidence.Features;
using Aw.Evidence.Infrastructure;
using Aw.Kernel.Json;
using Aw.Kernel.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Evidence.Host;

/// <summary>The one composition root, shared by Program.cs and the DI validation test.</summary>
internal static class Composition
{
    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .AddKernelTelemetry(TelemetryOptions.FromEnvironment("ui-evidence", environment, clock))   // JSON lines on stderr (AW_LOG_LEVEL); exports off
            .AddSingleton(AwJson.CreateOptions(EvidenceJsonContext.Default))   // the one Kernel JSON profile: output and run files
            .AddEvidenceCore()
            .AddEvidenceFeatures()
            .AddEvidenceInfrastructure(environment)
            .AddEvidencePostgresInfrastructure();
}
