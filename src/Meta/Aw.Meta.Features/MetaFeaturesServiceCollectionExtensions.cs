using Aw.Kernel.Services;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Worktrees;
using Aw.Meta.Features.CheckMerge;
using Aw.Meta.Features.GateRerun;
using Aw.Meta.Features.GateRun;
using Aw.Meta.Features.Health;
using Aw.Meta.Features.Inbox;
using Aw.Meta.Features.InboxDefer;
using Aw.Meta.Features.InboxDismiss;
using Aw.Meta.Features.LaneInbox;
using Aw.Meta.Features.LaneReply;
using Aw.Meta.Features.Lanes;
using Aw.Meta.Features.LaneSend;
using Aw.Meta.Features.LaneStopCheck;
using Aw.Meta.Features.LogAppend;
using Aw.Meta.Features.MergeRun;
using Aw.Meta.Features.OutboxRecord;
using Aw.Meta.Features.Owner;
using Aw.Meta.Features.OwnerAnswer;
using Aw.Meta.Features.OwnerAsk;
using Aw.Meta.Features.QueueDrop;
using Aw.Meta.Features.QueueGate;
using Aw.Meta.Features.Roles;
using Aw.Meta.Features.ShowVersion;
using Aw.Meta.Features.Status;
using Aw.Meta.Features.StopCheck;
using Aw.Meta.Features.SyncInbox;
using Aw.Meta.Features.Team;
using Aw.Meta.Features.Watch;
using Aw.Meta.Features.WorktreeList;
using Aw.Meta.Features.WorktreeSweep;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Features;

/// <summary>Registers Aw.Meta.Features's slices: each service, its validator and its decorators (outermost first).</summary>
public static class MetaFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Meta use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddMetaFeatures(this IServiceCollection services) => services
        .AddSetupSlices()
        .AddInboxSlices()
        .AddHookSlices()
        .AddOwnerSlices()
        .AddReportSlices()
        .AddGateSlices()
        .AddLaneSlices()
        .AddHostSlices()
        .AddWorktreeSlices();

    private static IServiceCollection AddSetupSlices(this IServiceCollection services) => services
        .AddScoped<SetRoleService>()
        .Plain<SetRoleService, SetRoleRequest, MetaRole>()
        .AddScoped<ShowRoleService>()
        .Plain<ShowRoleService, ShowRoleRequest, ShowRoleResponse>()
        .AddScoped<TeamInitService>()
        .AddScoped<IValidator<TeamInitRequest>, TeamInitValidator>()
        .Validated<TeamInitService, TeamInitRequest, TeamConfig>()
        .AddScoped<TeamShowService>()
        .Plain<TeamShowService, TeamShowRequest, TeamConfig>()
        .AddScoped<ShowVersionService>()
        .Plain<ShowVersionService, ShowVersionRequest, BuildInfo>()
        .AddScoped<TeamListService>()
        .Plain<TeamListService, TeamListRequest, TeamListResponse>()
        .AddScoped<TeamMigrateService>()
        .Plain<TeamMigrateService, TeamMigrateRequest, TeamMigration>();

    private static IServiceCollection AddInboxSlices(this IServiceCollection services) => services
        .AddScoped<SyncInboxService>()
        .AddScoped<IValidator<SyncInboxRequest>, SyncInboxValidator>()
        .Validated<SyncInboxService, SyncInboxRequest, SyncInboxResponse>()
        .AddScoped<ListInboxService>()
        .Plain<ListInboxService, ListInboxRequest, ListInboxResponse>()
        .AddScoped<DismissInboxService>()
        .AddScoped<IValidator<DismissInboxRequest>, DismissInboxValidator>()
        .Validated<DismissInboxService, DismissInboxRequest, DismissInboxResponse>()
        .AddScoped<DeferInboxService>()
        .AddScoped<IValidator<DeferInboxRequest>, DeferInboxValidator>()
        .Validated<DeferInboxService, DeferInboxRequest, DeferInboxResponse>();

    private static IServiceCollection AddHookSlices(this IServiceCollection services) => services
        .AddScoped<StopCheckService>()
        .AddScoped<IValidator<StopCheckRequest>, StopCheckValidator>()
        .Validated<StopCheckService, StopCheckRequest, StopCheckResponse>()
        .AddScoped<OutboxRecordService>()
        .AddScoped<IValidator<OutboxRecordRequest>, OutboxRecordValidator>()
        .Validated<OutboxRecordService, OutboxRecordRequest, OutboxRecordResponse>()
        .AddScoped<RefreshLanesService>()
        .Plain<RefreshLanesService, RefreshLanesRequest, LanesResponse>();

    private static IServiceCollection AddLaneSlices(this IServiceCollection services) => services
        .AddScoped<LaneSendService>()
        .AddScoped<IValidator<LaneSendRequest>, LaneSendValidator>()
        .Validated<LaneSendService, LaneSendRequest, LaneSendResponse>()
        .AddScoped<LaneInboxService>()
        .Plain<LaneInboxService, LaneInboxRequest, LaneInboxResponse>()
        .AddScoped<LaneStopCheckService>()
        .AddScoped<IValidator<LaneStopCheckRequest>, LaneStopCheckValidator>()
        .Validated<LaneStopCheckService, LaneStopCheckRequest, LaneStopCheckResponse>()
        .AddScoped<LaneReplyService>()
        .Plain<LaneReplyService, LaneReplyRequest, LaneReplyResponse>();

    private static IServiceCollection AddOwnerSlices(this IServiceCollection services) => services
        .AddScoped<ListOwnerService>()
        .Plain<ListOwnerService, ListOwnerRequest, ListOwnerResponse>()
        .AddScoped<AnswerOwnerService>()
        .AddScoped<IValidator<AnswerOwnerRequest>, AnswerOwnerValidator>()
        .Validated<AnswerOwnerService, AnswerOwnerRequest, AnswerOwnerResponse>()
        .AddScoped<AskOwnerService>()
        .AddScoped<IValidator<AskOwnerRequest>, AskOwnerValidator>()
        .Validated<AskOwnerService, AskOwnerRequest, AskOwnerResponse>();

    private static IServiceCollection AddReportSlices(this IServiceCollection services) => services
        .AddScoped<AppendLogService>()
        .AddScoped<IValidator<AppendLogRequest>, AppendLogValidator>()
        .Validated<AppendLogService, AppendLogRequest, AppendLogResponse>()
        .AddScoped<QueueDropService>()
        .AddScoped<IValidator<QueueDropRequest>, QueueDropValidator>()
        .Validated<QueueDropService, QueueDropRequest, QueueDropResponse>()
        .AddScoped<StatusReader>()
        .AddScoped<StatusService>()
        .Plain<StatusService, StatusRequest, StatusResponse>();

    private static IServiceCollection AddGateSlices(this IServiceCollection services) => services
        .AddScoped<RerunExecutor>()
        .AddScoped<GateRunService>()
        .AddScoped<IValidator<GateRunRequest>, GateRunValidator>()
        .Validated<GateRunService, GateRunRequest, GateRunResponse>()
        .AddScoped<QueueGateService>()
        .AddScoped<IValidator<QueueGateRequest>, QueueGateValidator>()
        .Validated<QueueGateService, QueueGateRequest, QueueGateResponse>()
        .AddScoped<GateRerunService>()
        .AddScoped<IValidator<GateRerunRequest>, GateRerunValidator>()
        .Validated<GateRerunService, GateRerunRequest, GateRerunResponse>()
        .AddScoped<CheckMergeService>()
        .AddScoped<IValidator<CheckMergeRequest>, CheckMergeValidator>()
        .Validated<CheckMergeService, CheckMergeRequest, CheckMergeResponse>()
        .AddScoped<MergeRunService>()
        .AddScoped<IValidator<MergeRunRequest>, MergeRunValidator>()
        .AddScoped<IService<MergeRunRequest, MergeRunResponse>>(static sp => new ValidatingService<MergeRunRequest, MergeRunResponse>(
            new SweepAfterMergeService(sp.GetRequiredService<MergeRunService>(), sp.GetRequiredService<IMetaConfig>(), sp.GetRequiredService<WorktreeSweeper>()),
            sp.GetRequiredService<IValidator<MergeRunRequest>>()));

    private static IServiceCollection AddHostSlices(this IServiceCollection services) => services
        .AddScoped<HostProbe>()
        .AddScoped<LaneCheck>()
        .AddScoped<OrphanKiller>()
        .AddScoped<HealthService>()
        .Plain<HealthService, HealthRequest, HealthResponse>()
        .AddScoped<WatchSweeper>()
        .AddScoped<WatchPoller>()
        .AddScoped<WatchService>()
        .AddScoped<IValidator<WatchRequest>, WatchValidator>()
        .Validated<WatchService, WatchRequest, WatchResponse>();

    private static IServiceCollection AddWorktreeSlices(this IServiceCollection services) => services
        .AddScoped<ListWorktreesService>()
        .Plain<ListWorktreesService, ListWorktreesRequest, ListWorktreesResponse>()
        .AddScoped<SweepWorktreesService>()
        .AddScoped<IValidator<SweepWorktreesRequest>, SweepWorktreesValidator>()
        .Validated<SweepWorktreesService, SweepWorktreesRequest, SweepWorktreesResponse>();

    // Exposes an already registered service as its IService.
    private static IServiceCollection Plain<TService, TRequest, TResponse>(this IServiceCollection services)
        where TService : class, IService<TRequest, TResponse>
        where TRequest : notnull =>
        services.AddScoped<IService<TRequest, TResponse>>(static sp => sp.GetRequiredService<TService>());

    // Exposes an already registered service, with its registered validator in front, as its IService.
    private static IServiceCollection Validated<TService, TRequest, TResponse>(this IServiceCollection services)
        where TService : class, IService<TRequest, TResponse>
        where TRequest : notnull =>
        services.AddScoped<IService<TRequest, TResponse>>(static sp =>
            new ValidatingService<TRequest, TResponse>(sp.GetRequiredService<TService>(), sp.GetRequiredService<IValidator<TRequest>>()));
}
