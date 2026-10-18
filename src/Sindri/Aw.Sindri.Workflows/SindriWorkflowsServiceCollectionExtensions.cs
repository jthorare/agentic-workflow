using Aw.Kernel;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Registers the Temporal adapter (engine spec rev 8 §0): the dev server, the activities, the worker options, the instance runtime and the stub item port.</summary>
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
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriWorkflows(this IServiceCollection services, Func<string, string?> environment)
    {
        var settings = TemporalSettings.FromEnvironment(environment);
        return services
            .AddSingleton(settings)
            .AddSingleton(new WorkflowsHome(settings.Home))
            .AddSingleton<TemporalDevServer>()
            .AddSingleton<ITemporalServer, DevServerTemporal>()
            .AddSingleton<RunStepActivities>()
            .AddSingleton<RecordEventActivities>()
            .AddSingleton<FactReadActivities>()
            .AddSingleton<EffectDispatch>(static provider => (context, ct) => DispatchScoped(provider, context, ct))   // engine P6 unit (a): RunEffect runs a scoped block by id
            .AddSingleton<BlockStepDispatch>(static provider => (step, context, ct) => RunBlockScoped(provider, step, context, ct))   // DT-J2: RunBlockStep runs a scoped block Step by id
            .AddSingleton<BlockStepRunner>().AddSingleton<AgentStepSelection>()
            .AddSingleton<EffectActivities>()                          // engine P6: RecordEffect, RecheckEffect, RunEffect
            .AddAgentRuntime()
            .AddSingleton(provider => SindriWorker.Options(settings, provider.GetRequiredService<WorkerActivities>()))
            .AddSingleton<TemporalFrontend>()
            .AddSingleton<KnownInstances>()                            // engine P4: the opaque ids this process has seen (spec §6.3)
            .AddSingleton<IItems, SelfItems>()                       // stub until API P3's items
            .AddSingleton<ILaunchRequests, LaunchRequestsView>()       // API P10a U9: the item's launch still requested, read from the launches view (UI E-19)
            .AddSingleton<IInstanceRuntime, TemporalInstanceRuntime>()
            .AddSingleton<IInstanceReader>(provider => provider.GetRequiredService<IInstanceRuntime>())   // the queries' read port (ARCH-CQRS-002)
            .AddSingleton<IWorkflowInstances, TemporalWorkflowInstances>()
            .AddSingleton<ITemporalWorker, SindriWorkerHost>()
            .AddSingleton<HomeFactStore>()
            .AddSingleton<IFactStore>(static sp => sp.GetRequiredService<HomeFactStore>())   // engine P4 unit (c): facts stored 0600 before the report
            .AddSingleton<IFactContents>(static sp => sp.GetRequiredService<HomeFactStore>()) // engine P6 unit (d): read back for effect.claim.ref
            .AddSingleton<ICommandNonces, HomeCommandNonces>()           // engine P4 unit (c): the nonce table (stand-in until API's IssueNonce)
            .AddSingleton<TemporalDeskReports>()                     // engine P4 unit (c): Send's 'complete' path
            .AddSingleton<TemporalCommands>()                          // engine P4 unit (b): Send's answer, command and cancel paths
            .AddSingleton<TemporalInstanceDirectory>()
            .AddSingleton<IInstanceDirectory>(static sp => sp.GetRequiredService<TemporalInstanceDirectory>())   // engine P4 unit (b): attempts and bare items
            .AddSingleton<IInstanceLiveness>(static sp => sp.GetRequiredService<TemporalInstanceDirectory>())    // engine P6 unit (d): a claim holder's liveness
            .AddSingleton<IRecipeWake, TemporalRecipeWake>()          // engine P6 integration: the revoke and drift wakes as the 'wake' signal (replaces P2's no-op)
            .AddSingleton<ProfileWorkflowTimes>()                    // engine P4 unit (e): S1-D11, workflow.deskTimeout and workflow.eventHold, read by LoadRecipe
            .AddSingleton<HeldItemEvents>()                            // engine P4: the bridge's held window (visibility lag)
            .AddSingleton<EventBridgeCursor>()                         // engine P4 unit (d): the bridge's cursor file
            .AddScoped<IScheduledService, EventBridge>()               // engine P4 unit (d): the ledger-to-Temporal bridge
            .AddSingleton(EventBridge.BuiltIn);                        // engine P4 unit (d): runs without a profile entry (S1-D8)
    }

    // Engine P8 U3: LaunchAgentStep, WatchAgentStep, CloseAgentStep and what they use, and the worker's set of activity classes.
    private static IServiceCollection AddAgentRuntime(this IServiceCollection services) => services
        .AddSingleton<AgentStepActivities>().AddSingleton<AgentLaunching>().AddSingleton<AgentClosing>()
        .AddSingleton<AgentTargetResolver>().AddSingleton<AgentLaunchRecorder>().AddSingleton(AgentRuntimeOptions.Default)
        .AddSingleton<IRunSuspensions, InstanceRunSuspensions>().AddSingleton<AgentRunFolder>()
        .AddSingleton<WorkerActivities>();

    private static async ValueTask<BlockStepResult> RunBlockScoped(IServiceProvider provider, BlockId step, StepContext context, CancellationToken cancellationToken)
    {
        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await scope.ServiceProvider.GetRequiredKeyedService<IBlockStep>(step.Value).Run(context, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The <see cref="EffectDispatch"/> registration: resolves the block AddEffect keyed by the effect's id, in its own scope.</summary>
    private static async ValueTask<Result<EffectDone>> DispatchScoped(IServiceProvider provider, EffectContext context, CancellationToken cancellationToken)
    {
        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await scope.ServiceProvider.GetRequiredKeyedService<IEffectKind>(context.Effect.Value).Dispatch(context, cancellationToken).ConfigureAwait(false);
        }
    }
}
