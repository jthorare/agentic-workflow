using Aw.Kernel;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
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
            .AddInterpreterActivities()
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
            .AddEventBridge();
    }

    /// <summary>The ledger-to-Temporal event bridge (its held window and cursor file) and the router stub (its cursor file and the 'item.routed' scheduled service), both of which run without a profile entry.</summary>
    private static IServiceCollection AddEventBridge(this IServiceCollection services) => services
            .AddSingleton<HeldItemEvents>()                            // engine P4: the bridge's held window (visibility lag)
            .AddSingleton<EventBridgeCursor>()                         // engine P4 unit (d): the bridge's cursor file
            .AddScoped<IScheduledService, EventBridge>()               // engine P4 unit (d): the ledger-to-Temporal bridge
            .AddSingleton(EventBridge.BuiltIn)                         // engine P4 unit (d): runs without a profile entry (S1-D8)
            .AddSingleton<RouterStubCursor>()                          // engine P7: the stub's cursor file
            .AddScoped<ItemRouter>()
            .AddScoped<IScheduledService, RouterStub>()                // engine P7 unit U3: the router stub (spec §12), 'item.routed' for each workflow.item that starts
            .AddSingleton(RouterStub.BuiltIn)
            .AddPrWatch();

    /// <summary>The 'pr-watch' poller (PR babysitter spec §6.2): follows the pull requests live instances wait on and appends 'pr.changed'; runs every 60 s without a profile entry.</summary>
    private static IServiceCollection AddPrWatch(this IServiceCollection services) => services
            .AddSingleton<IWatchedPulls, InstanceWatchedPulls>()      // reads the open instances that wait on pr.changed and hold a fact:pr
            .AddSingleton<IPullDigests, ForgePullDigests>()           // the one caller of the forge port's digest
            .AddSingleton<IPrWatchState, HomePrWatchState>()          // <home>/workflow/pr-watch.json
            .AddSingleton<IPrRemindAfter, ProfilePrRemindAfter>()     // workflow.pr.remindAfter
            .AddScoped<PrEventWriter>()
            .AddScoped<IScheduledService, PrWatchService>()
            .AddSingleton(PrWatchService.BuiltIn);

    /// <summary>The activities the interpreter runs (RunStep, RecordEvent, ReadItemRepo, the effect activities, EvaluateGates) and the Step, effect and gate dispatchers they run blocks through.</summary>
    private static IServiceCollection AddInterpreterActivities(this IServiceCollection services) => services
        .AddSingleton<RunStepActivities>()
        .AddSingleton<RecordEventActivities>()
        .AddScoped<ScopingApprovals>()                             // BS-1b: the scoping.approved row after step.scope.freeze reports frozen
        .AddSingleton<FactReadActivities>()
        .AddSingleton<EffectDispatch>(static provider => (context, ct) => DispatchScoped(provider, context, ct))   // engine P6 unit (a): RunEffect runs a scoped block by id
        .AddSingleton<BlockStepDispatch>(static provider => (step, context, ct) => RunBlockScoped(provider, step, context, ct))   // DT-J2: RunBlockStep runs a scoped block Step by id
        .AddSingleton<BlockStepRunner>().AddSingleton<AgentStepSelection>()
        .AddSingleton<GateDispatch>(static provider => (gate, context, ct) => RunGateScoped(provider, gate, context, ct))   // engine P7: EvaluateGates runs a scoped gate by id
        .AddSingleton<GateActivities>()                            // engine P7: EvaluateGates
        .AddSingleton<CleanupActivities>()                         // engine P7: ReleaseClaim
        .AddSingleton<ChildActivities>()                           // engine P7: LoadChildren
        .AddSingleton<HeavyLeases>().AddSingleton<HeavyActivities>()   // LN-WF3: TakeHeavy, ReleaseHeavy over IHeavySlots; the leases a worker holds
        .AddSingleton<EngineActivities>()
        .AddSingleton<EffectActivities>()                          // engine P6: RecordEffect, RecheckEffect, RunEffect
        .AddStepInputRuntime()                                     // the Step inputs: FillInput, the in-memory texts and their runner
        .AddSingleton<WorkerActivities>();

    /// <summary>The <see cref="GateDispatch"/> registration: resolves the gate AddGate keyed by its id, in its own scope; null when this build has no runtime for it.</summary>
    private static async ValueTask<GateResult?> RunGateScoped(IServiceProvider provider, BlockId gate, GateContext context, CancellationToken cancellationToken)
    {
        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return scope.ServiceProvider.GetKeyedService<IGate>(gate.Value) is { } runtime
                ? await runtime.Evaluate(context, cancellationToken).ConfigureAwait(false)
                : null;
        }
    }

    // Engine P8 U3: LaunchAgentStep, WatchAgentStep, CloseAgentStep and what they use, and the worker's set of activity classes.
    private static IServiceCollection AddAgentRuntime(this IServiceCollection services) => services
        .AddSingleton<AgentStepActivities>().AddSingleton<AgentLaunching>().AddSingleton<AgentClosing>()
        .AddSingleton<UsageFlush>(static provider => (launch, ct) => FlushScoped(provider, launch, ct))
        .AddSingleton<AgentTargetResolver>().AddSingleton<AgentLaunchRecorder>().AddSingleton(AgentRuntimeOptions.Default)
        .AddSingleton<WorkerActivities>();

    private static async ValueTask FlushScoped(IServiceProvider provider, LaunchId launch, CancellationToken cancellationToken)
    {
        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            _ = await scope.ServiceProvider.GetRequiredService<ISessionUsageSampler>().Flush(launch, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask<BlockStepResult> RunBlockScoped(IServiceProvider provider, BlockId step, StepContext context, CancellationToken cancellationToken)
    {
        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var result = await scope.ServiceProvider.GetRequiredKeyedService<IBlockStep>(step.Value).Run(context, cancellationToken).ConfigureAwait(false);
            return step == ScopingApprovals.Freeze   // BS-1b: the freeze Step has no ledger port, so the engine appends scoping.approved for its 'frozen'
                ? await scope.ServiceProvider.GetRequiredService<ScopingApprovals>().After(step, context, result, cancellationToken).ConfigureAwait(false)
                : result;
        }
    }

    /// <summary>The Step inputs' run time: the in-memory texts (no text is stored), the one runner, the FillInput activity, and the in-process Step's context (no IStep runtime ships yet).</summary>
    private static IServiceCollection AddStepInputRuntime(this IServiceCollection services) => services
        .AddSingleton<IInjectionTexts, MemoryInjectionTexts>()
        .AddSingleton<InputDispatch>(static provider => (context, ct) => FillScoped(provider, context, ct))
        .AddSingleton<IStepInputRunner, StepInputRunner>()
        .AddSingleton<IInjectionSources, TemporalInjectionSources>()
        .AddSingleton<StepInputActivities>()
        .AddSingleton<StepContexts>();

    private static async ValueTask<Result<InjectionSections>> FillScoped(IServiceProvider provider, StepInputContext context, CancellationToken cancellationToken)
    {
        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await scope.ServiceProvider.GetRequiredKeyedService<IStepInput>(context.Input.Value).Fill(context, cancellationToken).ConfigureAwait(false);
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
