using System.Diagnostics;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Registers the Temporal adapter (engine spec rev 8 §0): the dev server, the agent runtime's activities and the worker options.</summary>
public static class SindriWorkflowsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the dev server (the <see cref="ITemporalServer"/> port 'sindri serve' starts and stops), the activities, the
    /// worker's options, the <see cref="IInstanceRuntime"/> the workflow verbs use, and engine P4's stub
    /// <see cref="IItems"/> (until API P3), all under the one Sindri home
    /// <see cref="TemporalSettings"/> reads.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (the Sindri home and the state root).</param>
    /// <param name="childStart">Makes the start info of a child process: the composition passes Infrastructure's child-environment factory (ARCH-PROC-001), so the dev server's shell gets the pod-mode environment.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriWorkflows(this IServiceCollection services, Func<string, string?> environment, Func<string, IEnumerable<string>?, IReadOnlyDictionary<string, string?>?, ProcessStartInfo> childStart)
    {
        var settings = TemporalSettings.FromEnvironment(environment);
        return services
            .AddSingleton(settings)
            .AddSingleton(new WorkflowsHome(settings.Home))
            .AddSingleton(new TemporalDevServer(childStart))
            .AddSingleton<ITemporalServer, DevServerTemporal>()
            .AddAgentRuntime()
            .AddSingleton(provider => SindriWorker.Options(settings, provider.GetRequiredService<WorkerActivities>()))
            .AddSingleton<TemporalFrontend>()
            .AddSingleton<ILaunchRequests, LaunchRequestsView>()       // API P10a U9: the item's launch still requested, read from the launches view (UI E-19)
            .AddSingleton<ITemporalWorker, SindriWorkerHost>()
            .AddSingleton<NoInstances>()                                // no workflow instance exists now: the reads that still ask find none
            .AddSingleton<IInstanceDirectory>(static sp => sp.GetRequiredService<NoInstances>())
            .AddSingleton<IInstanceRuntime>(static sp => sp.GetRequiredService<NoInstances>())
            .AddSingleton<IInstanceReader>(static sp => sp.GetRequiredService<NoInstances>())
            .AddSingleton<HomeFactStore>()
            .AddSingleton<IFactStore>(static sp => sp.GetRequiredService<HomeFactStore>())
            .AddSingleton<IFactContents>(static sp => sp.GetRequiredService<HomeFactStore>());   // the agent target resolver reads stored facts
    }

    // Engine P8 U3: LaunchAgentStep, WatchAgentStep, CloseAgentStep and what they use, and the worker's set of activity classes.
    private static IServiceCollection AddAgentRuntime(this IServiceCollection services)
    {
        services.TryAddSingleton<ReportRefusals>();
        services.TryAddSingleton<IReportOutputChecks, NoReportOutputChecks>();
        services.TryAddSingleton<IModelPickSettings, NoModelPickSettings>();   // the profile adapter replaces these two
        services.TryAddSingleton<IModelPickHistory, NoModelPickHistory>();
        return services
        .AddSingleton<AgentStepActivities>().AddSingleton<AgentLaunching>().AddSingleton<AgentLaunchWaiting>().AddSingleton<ModelPicker>().AddSingleton<ModelPickStore>().AddSingleton<ChallengerPick>().AddSingleton<AgentPlacing>().AddSingleton<AgentLaunchInputs>().AddSingleton<AgentClosing>()
        .AddSingleton<AgentRecovering>().AddSingleton<SkillStepWatching>().AddSingleton<AgentStepSelection>().AddSingleton<IStepOutputs, DiskStepOutputs>()
        .AddSingleton<UsageFlush>(static provider => (launch, ct) => FlushScoped(provider, launch, ct))
        .AddSingleton<AgentTargetResolver>().AddSingleton<AgentLaunchRecorder>().AddSingleton(AgentRuntimeOptions.Default)
        .AddSingleton<WorkerActivities>();
    }

    private static async ValueTask FlushScoped(IServiceProvider provider, LaunchId launch, CancellationToken cancellationToken)
    {
        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            _ = await scope.ServiceProvider.GetRequiredService<ISessionUsageSampler>().Flush(launch, cancellationToken).ConfigureAwait(false);
        }
    }
}
