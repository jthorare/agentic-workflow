using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Usage;
using Aw.Meta.Core.Worktrees;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Core.Registration;

/// <summary>Registers Aw.Meta.Core's types.</summary>
public static class MetaCoreServiceCollectionExtensions
{
    /// <summary>Adds the Meta domain services that the slices share: the inbox sync, the lane refresher, the obligation reader, the meta-session check, the sweep and the stop guard.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddMetaCore(this IServiceCollection services) => services
        .AddScoped<InboxSync>()
        .AddScoped<InboxCounts>()
        .AddScoped<LaneRefresher>()
        .AddScoped<ObligationReader>()
        .AddScoped<MetaSession>()
        .AddScoped<MetaSweep>()
        .AddScoped<StopGuard>()
        .AddScoped<LaneBeats>()
        .AddScoped<LaneStopGuard>()
        .AddScoped<LaneHealthReader>()
        .AddScoped<LaneContextReader>()
        .AddScoped<PaneSubmitter>()
        .AddScoped<UsageReader>()
        .AddScoped<UsageWatcher>()
        .AddScoped<UsageMonitor>()
        .AddScoped<RepoCommands>()
        .AddScoped<GateTree>()
        .AddScoped<GateWorkspace>()
        .AddScoped<GateStepRunner>()
        .AddScoped<TrainSelection>()
        .AddScoped<MergeReadiness>()
        .AddScoped<MergeQueueReader>()
        .AddScoped<HeavyReservation>()
        .AddScoped<ResourceAdvisor>()
        .AddScoped<WorktreeInspector>()
        .AddScoped<WorktreeSweeper>();
}
