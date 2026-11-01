using Aw.CodeIndex.Features;
using Aw.CodeIndex.Infrastructure;
using Aw.Connectors.Features;
using Aw.Connectors.Infrastructure;
using Aw.Context.Core;
using Aw.Context.Core.Models;
using Aw.Context.Features;
using Aw.Context.Infrastructure;
using Aw.Kernel.Json;
using Aw.Kernel.Telemetry;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Registration;
using Aw.Sindri.Features;
using Aw.Sindri.Host.Forge;
using Aw.Sindri.Host.Models;
using Aw.Sindri.Host.Registration;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Registration;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Host;

/// <summary>The one composition root, shared by Program.cs and the DI validation test: only the projects' Add…() calls, the clock and the JSON options.</summary>
internal static class Composition
{
    /// <summary>The child-process factory (Infrastructure's), for the Host code that starts a process but may not use Infrastructure itself.</summary>
    /// <param name="fileName">The executable.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>The start info with the pod-mode child environment applied.</returns>
    internal static System.Diagnostics.ProcessStartInfo StartChild(string fileName, IEnumerable<string>? arguments = null) =>
        ChildEnvironment.Start(fileName, arguments);

    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .AddSingleton(AwJson.CreateOptions(SindriJsonContext.Default))
            .AddKernelTelemetry(TelemetryOptions.FromEnvironment("sindri", environment, clock))   // JSON lines on stderr (AW_LOG_LEVEL); exports off
            .AddSindriCore()
            .AddSindriFeatures()
            .AddSindriInfrastructure(environment)
            .AddProfileRecheck()   // wraps the profile writer ports Infrastructure just registered: Sindri's own writes record a profile check at once (#1826)
            .AddSindriPlatformSandbox(environment)   // SB-5a: the one switch (bubblewrap + session cap on Linux, UnavailableSandbox elsewhere)
            .AddSindriWorkflows(environment, ChildEnvironment.Start)   // the Temporal dev server serve keeps beside itself (engine spec rev 8 §0), the worker and 'workflow complete'
            .AddContextCore()
            .AddContextFeatures()
            .AddContextInfrastructure(ContextEnvironment(environment))   // context engine: $AW_SINDRI_HOME/context (compose edge); pointers resolve here
            .AddCodeIndexFeatures()
            .AddCodeIndexInfrastructure(environment)   // the code index: $AW_SINDRI_HOME/code-index (compose edge)
            .AddSlackConnectorFactory()
            .AddGitHubConnectorFactory()   // the source readers are built per token (owned-index spec §9.2), so only the factory; the Host adapter over IConnector is IndexSources/ConnectorFeed
            .AddConnectorsInfrastructure(environment)
            .AddContextLabeling()   // the labeling pass over IModelPort; a build asks it only when the approved profile turns 'context.labeling' on
            .AddSingleton<IApprovedModels, ApprovedModels>()   // 'context.models' of the approved profile, read once per approved hash (model routing spec §4)
            .AddSingleton<IModelCallLog, LedgerModelCallLog>()   // the router's 'context.model.called' event, as service:context, best effort
            .AddSingleton(sp => new JudgeCatalog(environment, sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<System.Text.Json.JsonSerializerOptions>()))   // Judge's provider catalog, built on first use; the router and the doctor's context:models row share it
            .AddSingleton<IModelPort>(sp => new RoutedModelPort(sp.GetRequiredService<JudgeCatalog>().Get,
                sp.GetRequiredService<IApprovedModels>(), sp.GetRequiredService<IJobContextAccessor>(), sp.GetRequiredService<IModelCallLog>(), sp.GetRequiredService<Kernel.Ids.IIdGenerator>(), sp.GetRequiredService<TimeProvider>()))
            .AddChoicePort(environment)   // the Jev label pick: IChoicePort over the judge's batch classifier, and the route's prompt budget (model routing spec §6.7)
            .AddScoped<IDoctorChecks>(sp => new ModelPickDoctorChecks(sp.GetRequiredService<IModelPickSettings>(), environment))   // the 'model:pick' row (model-pick spec 8)
            .AddScoped<IDoctorChecks>(sp => new ModelRoutingDoctorChecks(sp.GetRequiredService<IApprovedModels>(), sp.GetRequiredService<IModelCatalog>(), () => RoutedModelPort.InstalledHarnesses(sp.GetRequiredService<JudgeCatalog>().Get())))   // the 'context:models' row (model routing spec 8.4)
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
