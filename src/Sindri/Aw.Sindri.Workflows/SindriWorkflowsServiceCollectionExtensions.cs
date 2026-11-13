using System.Diagnostics;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
            .AddGraphEngine()
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

    /// <summary>The graph engine (issue 2258, PR 6): the run starter and its Temporal launcher, the queue advance, the blocks and the activities of <see cref="GraphWorkflow"/>.</summary>
    private static IServiceCollection AddGraphEngine(this IServiceCollection services)
    {
        return services
            .AddSingleton<GraphMirror>()
            .AddSingleton<IForEachLimitSource, ProfileForEachLimits>()
            .AddSingleton<IGraphCallPort, GraphWorkflowCaller>()   // 'run_workflow' starts the other workflow's active revision (agent-first PR 9)
            .AddSingleton<IGraphResultPort, GraphRunResults>()   // 'workflow_result' reads a finished run (agent-first PR 9)
            .AddSingleton(static provider => GraphBlockRegistry.Standard(provider.GetRequiredService<INotifyPort>(), provider.GetRequiredService<IGraphModelPort>(), provider.GetRequiredService<IForEachLimitSource>(),
                [.. GraphBlockRegistry.FlowBlocks(provider.GetRequiredService<IGraphSeenStore>(), provider.GetRequiredService<ICreateItemPort>(), provider.GetRequiredService<IGraphCallPort>(), provider.GetRequiredService<IGraphHttpPort>()),
                 .. GraphBlockRegistry.ResultBlocks(provider.GetRequiredService<IGraphResultPort>()),
                 .. GraphBlockRegistry.ParkingBlocks(provider.GetRequiredService<IQuestionPort>(), provider.GetRequiredService<TimeProvider>()),
                 .. GraphBlockRegistry.MemoryBlocks(provider.GetRequiredService<IGraphMemoryPort>()),
                 new CallToolBlock(provider.GetService<IRunningToolkits>() ?? NoToolkits.Instance, provider.GetRequiredService<IConnectionToolPort>()),   // 'call_tool' (PR 16b); with no toolkit runtime registered yet it reaches connections only
                 .. GraphBlockRegistry.CodeBlocks(provider.GetRequiredService<ICodeRunPort>())]))   // 'code' (PR 9c): C# in the sandboxed sindri-code-runner
            .AddSingleton(static provider => new TemporalGraphRunLauncher(provider.GetRequiredService<TemporalFrontend>(), provider.GetRequiredService<TemporalSettings>()))
            .AddSingleton<IGraphBlockCatalog>(static provider => provider.GetRequiredService<GraphBlockRegistry>())   // the validator reads what the engine runs, replacing the empty catalog
            .AddSingleton<IGraphRunLauncher>(static provider => provider.GetRequiredService<TemporalGraphRunLauncher>())
            .AddSingleton<IAgentNodeControl>(static provider => new TemporalAgentNodeControl(provider.GetRequiredService<TemporalFrontend>()))
            .AddSingleton<IGraphRunCanceller>(static provider => new TemporalGraphRunCanceller(provider.GetRequiredService<TemporalFrontend>(), provider.GetRequiredService<IWorkflowRunStore>()))
            .AddSingleton<IGraphRunAnswerer>(static provider => new TemporalGraphRunAnswerer(provider.GetRequiredService<TemporalFrontend>()))   // the 'Answer' update of an ask_owner node (PR 9)
            .AddSingleton<GraphRunStarter>()
            .AddSingleton<IGraphRunStarter>(static provider => provider.GetRequiredService<GraphRunStarter>())
            .AddSingleton<IGraphQueueAdvance>(static provider => provider.GetRequiredService<GraphRunStarter>())
            .AddSingleton<IGraphRunProbe>(static provider => new TemporalGraphRunProbe(provider.GetRequiredService<TemporalFrontend>()))
            .AddSingleton<GraphRunRecovery>()
            .AddSingleton<IGraphRunRecovery>(static provider => provider.GetRequiredService<GraphRunRecovery>())
            .AddSingleton(static provider => new GraphRunItemRepair(provider.GetService<IRunItemRepair>(), provider.GetRequiredService<ILogger<GraphRunItemRepair>>()))
            .AddSingleton<GraphRunActivities>().AddSingleton<GraphBlockActivities>().AddSingleton<GraphActivities>();
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
        .AddSingleton<AgentLaunchEnded>().AddSingleton<AgentLaunchStart>().AddSingleton<AgentNodeReading>().AddSingleton<AgentNodeLaunching>().AddSingleton<AgentSessionWatch>().AddSingleton<AgentNodeRecovering>().AddSingleton<AgentNodeClosing>().AddSingleton<AgentNodeAsking>()   // PR 7: a 'run_agent' node's runtime
        .AddSingleton<IAgentNodeRuntime, AgentNodeRuntime>().AddSingleton<AgentNodeActivities>()
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
