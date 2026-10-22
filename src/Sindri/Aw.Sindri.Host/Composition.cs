using Aw.Context.Core;
using Aw.Context.Core.Models;
using Aw.Context.Features;
using Aw.Context.Infrastructure;
using Aw.Kernel.Json;
using Aw.Kernel.Telemetry;
using Aw.Sindri.Core.Registration;
using Aw.Sindri.Features;
using Aw.Sindri.Host.Forge;
using Aw.Sindri.Host.Models;
using Aw.Sindri.Host.Registration;
using Aw.Sindri.Infrastructure.Registration;
using Aw.Sindri.Infrastructure.Workflows;

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
            .AddSindriWorkflows(environment)   // the Temporal dev server serve keeps beside itself (engine spec rev 8 §0), the worker and 'workflow complete'
            .AddContextCore()
            .AddContextFeatures()
            .AddContextInfrastructure(ContextEnvironment(environment))   // context engine: $AW_SINDRI_HOME/context (compose edge); pointers resolve here
            .AddContextLabeling()   // the labeling pass over IModelPort; a build asks it only when the approved profile turns 'context.labeling' on
            .AddSingleton<IModelPort>(sp => new JudgeModelPort(new JudgeCatalog(environment, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<System.Text.Json.JsonSerializerOptions>()).Get))
            .AddPullRequests(environment)   // IPullRequests over the forge the approved profile names (workflow.forge; compose edge to Aw.Forge.Infrastructure in Forge/ForgeAdapters)
            .AddProveBlocks(environment)   // input.prove.echo, only under AW_SINDRI_PROVE_BLOCKS=1 in a prover home
            .AddSindriHost();

    private static IServiceCollection AddProveBlocks(this IServiceCollection services, Func<string, string?> environment) =>
        SindriProveInfrastructureServiceCollectionExtensions.ProveBlocksAllowed(environment)
            ? services.AddSindriProveInfrastructure(environment).AddSindriProveBlocks()
            : services;

    /// <summary>
    /// The environment function Composition passes to AddContextInfrastructure (context spec §4 "Secrets"): a name that is a
    /// secret pointer (keychain:, env:, op:, file:) resolves here, in Sindri Host, so Aw.Context never resolves pointers or
    /// reads the keychain; every other name reads <paramref name="environment"/>.
    /// </summary>
    /// <param name="environment">The process environment (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The wrapped environment function.</returns>
    internal static Func<string, string?> ContextEnvironment(Func<string, string?> environment)
        => SecretPointerEnvironment.Over(environment, SindriInfrastructureServiceCollectionExtensions.CreateSecretPointers(environment));
}
