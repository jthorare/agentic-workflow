using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Core.Registration;

/// <summary>Registers Aw.Sindri.Core's types.</summary>
public static class SindriCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Sindri domain services: the control plane's event scope and factory, the caller of the code running now,
    /// the lease and human token this process holds, the Temporal runtime serve keeps, the projector's progress (status,
    /// rebuild control and feed) and its projections, the registry's approve and revoke planners, its reader and its
    /// event appender, and the approved recipes the engine reads over the registry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriCore(this IServiceCollection services)
        => services
            .AddSingleton(EventScope.ControlPlane)
            .AddSingleton<EventFactory>()
            .AddSingleton<ICallerContextAccessor, AsyncLocalCallerContextAccessor>()
            .AddSingleton<IJobContextAccessor, AsyncLocalJobContextAccessor>()
            .AddSingleton<HeldLease>()
            .AddSingleton<LiveSchedule>()
            .AddSingleton<EndedInstanceViews>()
            .AddSingleton<TemporalRuntime>()
            .AddSingleton<HeldHumanToken>()
            .AddSingleton<BlockCatalog>()
            .AddScoped<OnyxFacts>()
            .AddSingleton<BlockStepRuntimes>()
            .AddSingleton<StepInputRuntimes>()
            .AddSingleton<ILedgerSignal, LedgerSignal>()
            .AddSingleton<IStreamSlots, StreamSlots>()
            .AddScoped<CallerEventView>()
            .AddSingleton<IItemRepos, LedgerItemRepos>()
            .AddSingleton<ProjectorProgress>()
            .AddSingleton<IProjectorStatus>(static sp => sp.GetRequiredService<ProjectorProgress>())
            .AddSingleton<IProjectorControl>(static sp => sp.GetRequiredService<ProjectorProgress>())
            .AddSingleton<IProjectorFeed>(static sp => sp.GetRequiredService<ProjectorProgress>())
            .AddProjections()
            .AddSingleton<ILaunchViews, LaunchViews>()
            .AddSingleton<LaunchLiveness>()
            .AddSingleton<UsageSamples>()
            .AddScoped<ReportedIdAdmission>()
            .AddScoped<LaunchSessionEvents>()
            .AddScoped<RecipeApprovalPlanner>()
            .AddScoped<Workflow.ApproveAll.ShippedApprovalPlans>()
            .AddScoped<RecipeRevokePlanner>()
            .AddScoped<IRecipeProfileFacts, ApprovedRecipeProfileFacts>()
            .AddScoped<RecipeRegistryReader>()
            .AddSingleton<RecipeEvents>()
            .AddSingleton<IPinRenewals, PinRenewals>()
            .AddSingleton<IApprovedRecipes, RegistryApprovedRecipes>();

    /// <summary>The views' projections, in the order the projector folds them.</summary>
    private static IServiceCollection AddProjections(this IServiceCollection services)
        => services
            .AddSingleton<IProjection, ServicesProjection>()
            .AddSingleton<IProjection, ProfileProjection>()
            .AddSingleton<IProjection, RequestsProjection>()
            .AddSingleton<IProjection, ItemsProjection>()
            .AddSingleton<IProjection, ItemAliasesProjection>()
            .AddSingleton<IProjection, ItemTimelineProjection>()
            .AddSingleton<IProjection, NeedsYouProjection>()
            .AddSingleton<IProjection, JobsProjection>()
            .AddSingleton<IProjection, LaunchesProjection>()
            .AddSingleton<IProjection, RecipeStatsProjection>()
            .AddSingleton<IProjection, CodeIndexProjection>();
}
