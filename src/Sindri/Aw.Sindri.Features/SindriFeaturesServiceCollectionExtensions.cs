using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Locks;
using Aw.Context.Core.Map;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.ApplyCommand;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.Blocks;
using Aw.Sindri.Features.CancelJob;
using Aw.Sindri.Features.CheckProfile;
using Aw.Sindri.Features.CloseItem;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.Decide;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.FileRequest;
using Aw.Sindri.Features.GetEventCatalog;
using Aw.Sindri.Features.GetInstance;
using Aw.Sindri.Features.GetInstanceEvents;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.GetItemTimeline;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Features.GetProfile;
using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.IssueNonce;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Features.JobProbe;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Features.ListDecisions;
using Aw.Sindri.Features.ListInstances;
using Aw.Sindri.Features.ListItems;
using Aw.Sindri.Features.ListJobs;
using Aw.Sindri.Features.ListOutbox;
using Aw.Sindri.Features.ListRecipes;
using Aw.Sindri.Features.ListRequests;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Features.OnyxStatus;
using Aw.Sindri.Features.PauseService;
using Aw.Sindri.Features.PingProcess;
using Aw.Sindri.Features.ReadLedger;
using Aw.Sindri.Features.RebuildViews;
using Aw.Sindri.Features.RecordHeartbeat;
using Aw.Sindri.Features.RegisterMcp;
using Aw.Sindri.Features.RegisterRecipe;
using Aw.Sindri.Features.ResumeService;
using Aw.Sindri.Features.RevokeRecipe;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.ShowRecipe;
using Aw.Sindri.Features.StartInstance;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Features.ValidateProfile;
using Aw.Sindri.Features.ValidateRecipe;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers Aw.Sindri.Features's slices: the command layer every client (CLI, API, UI) calls (factory addendum §17.27.1).</summary>
public static class SindriFeaturesServiceCollectionExtensions
{
    /// <summary>
    /// Adds every Sindri use case as an IService: commands and queries behind their chains (API spec §2.1), the host's
    /// own services and the CLI-local verbs as plain services, the background services as IScheduledService, and the
    /// workflow blocks this build ships.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriFeatures(this IServiceCollection services) =>
        services.AddServices().AddCommands().AddQueries();

    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        _ = services
            .AddScoped<IItemResolver, ViewItemResolver>()
            .AddScoped<RequestTarget>()
            .AddScoped<IService<StartProcessRequest, StartProcessResponse>, StartProcessService>()
            .AddScoped<IService<RecordHeartbeatRequest, RecordHeartbeatResponse>, RecordHeartbeatService>()
            .AddRecordServiceRun()
            .AddScoped<IService<CheckProfileRequest, CheckProfileResponse>, CheckProfileService>()
            .AddScoped<IService<ValidateProfileRequest, ValidateProfileResponse>, ValidateProfileService>()
            .AddDoctor()
            .AddOnyxStatus()
            .AddScoped<ServeRecords>()
            .AddScoped<IService<RegisterMcpRequest, RegisterMcpResponse>, RegisterMcpService>()
            .AddScoped<IScheduledService, HeartbeatService>()
            .AddScoped<IScheduledService, JobProbeService>()   // opt-in: not a BuiltInService, so only a profile that names it schedules it
            .AddSingleton(static sp => new JobStartRecorder(sp.GetRequiredService<HeldLease>(), sp.GetRequiredService<ILedgerStore>(), sp.GetRequiredService<EventFactory>()))
            .AddSingleton(static sp => new JobEventWriter(sp.GetRequiredService<ILedgerStore>(), sp.GetRequiredService<HeldLease>(), sp.GetRequiredService<EventFactory>(),
                sp.GetRequiredService<ITextScrubber>(), sp.GetRequiredService<ICallerContextAccessor>()))   // API P6 unit C: the job runtime, outside the command gate
            .AddSingleton(static sp => new JobRunner(() => { var scope = sp.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope(); return new JobScope(scope.ServiceProvider.GetRequiredService, scope); }, sp.GetRequiredService<IJobContextAccessor>(),
                sp.GetRequiredService<ICallerContextAccessor>(), sp.GetRequiredService<JobEventWriter>(), sp.GetRequiredService<JobRegistry>(), sp.GetRequiredService<IShutdown>()))
            .AddSingleton(static sp => new JobRecovery(sp.GetRequiredService<IJobViews>(), sp.GetRequiredService<ILedgerReader>(), sp.GetRequiredService<IProjectorStatus>(),
                sp.GetRequiredService<HeldLease>(), sp.GetRequiredService<JobEventWriter>(), sp.GetRequiredService<JobRegistry>()))
            .AddSingleton<IJobRunner>(static sp => sp.GetRequiredService<JobRunner>()).AddSingleton<IJobCancellation>(static sp => sp.GetRequiredService<JobRunner>())
            .AddScoped<IService<ValidateRecipeRequest, ValidateRecipeResponse>, ValidateRecipeService>()
            .AddScoped<IService<EvalRecipeRequest, EvalRecipeResponse>, EvalRecipeService>()
            .AddRecipeBlocks()
            .AddRegistrySlices()
            .AddContextSlices()
            .AddContextJobs()
            .AddEngineSlices();
        return services;
    }

    private static IServiceCollection AddCommands(this IServiceCollection services)
    {
        // ---- commands (one line per slice) ----
        _ = services.AddScoped<IValidator<CancelJobRequest>, AcceptAllValidator<CancelJobRequest>>().AddCommand<CancelJobRequest, CommandAccepted, CancelJobService>();
        _ = services.AddScoped<IValidator<PauseServiceRequest>, AcceptAllValidator<PauseServiceRequest>>().AddCommand<PauseServiceRequest, CommandAccepted, PauseServiceService>();
        _ = services.AddScoped<IValidator<ResumeServiceRequest>, AcceptAllValidator<ResumeServiceRequest>>().AddCommand<ResumeServiceRequest, CommandAccepted, ResumeServiceService>();
        _ = services.AddRunService();
        _ = services.AddScoped<IValidator<CloseItemRequest>, CloseItemValidator>().AddCommand<CloseItemRequest, CommandAccepted, CloseItemService>();
        _ = services.AddScoped<IValidator<DecideRequest>, DecideValidator>().AddCommand<DecideRequest, CommandAccepted, DecideService>();
        _ = services.AddScoped<IValidator<FileRequestRequest>, FileRequestValidator>().AddCommand<FileRequestRequest, RequestFiledResponse, FileRequestService>();
        _ = services.AddScoped<IValidator<CreateItemRequest>, CreateItemValidator>().AddCommand<CreateItemRequest, ItemCreatedResponse, CreateItemService>();
        _ = services.AddScoped<IValidator<RebuildViewsRequest>, AcceptAllValidator<RebuildViewsRequest>>().AddCommand<RebuildViewsRequest, CommandAccepted, RebuildViewsService>();
        _ = services.AddScoped<IValidator<ImportContextMapRequest>, AcceptAllValidator<ImportContextMapRequest>>().AddCommand<ImportContextMapRequest, ImportContextMapResponse, ImportContextMapService>();
        _ = services.AddScoped<IValidator<ApproveProfileRequest>, AcceptAllValidator<ApproveProfileRequest>>().AddCommand<ApproveProfileRequest, ApproveProfileResponse, ApproveProfileService>();
        _ = services.AddScoped<IValidator<StopProcessRequest>, AcceptAllValidator<StopProcessRequest>>().AddCommand<StopProcessRequest, StopProcessResponse, StopProcessService>();
        return services;
    }

    private static IServiceCollection AddQueries(this IServiceCollection services)
    {
        // ---- queries (one line per slice) ----
        _ = services.AddScoped<IValidator<GetEventCatalogRequest>, AcceptAllValidator<GetEventCatalogRequest>>().AddQuery<GetEventCatalogRequest, EventCatalogView, GetEventCatalogService>();
        _ = services.AddScoped<IValidator<GetNeedsYouRequest>, AcceptAllValidator<GetNeedsYouRequest>>().AddQuery<GetNeedsYouRequest, GetNeedsYouResponse, GetNeedsYouService>();
        _ = services.AddScoped<IValidator<ListDecisionsRequest>, AcceptAllValidator<ListDecisionsRequest>>().AddQuery<ListDecisionsRequest, ListDecisionsResponse, ListDecisionsService>();
        _ = services.AddScoped<IValidator<ListRequestsRequest>, AcceptAllValidator<ListRequestsRequest>>().AddQuery<ListRequestsRequest, ListRequestsResponse, ListRequestsService>();
        _ = services.AddScoped<IValidator<GetJobRequest>, AcceptAllValidator<GetJobRequest>>().AddQuery<GetJobRequest, JobView, GetJobService>();
        _ = services.AddScoped<IValidator<ListJobsRequest>, AcceptAllValidator<ListJobsRequest>>().AddQuery<ListJobsRequest, JobList, ListJobsService>();
        _ = services.AddScoped<IValidator<GetItemRequest>, AcceptAllValidator<GetItemRequest>>().AddQuery<GetItemRequest, ItemView, GetItemService>();
        _ = services.AddScoped<IValidator<GetItemTimelineRequest>, AcceptAllValidator<GetItemTimelineRequest>>().AddQuery<GetItemTimelineRequest, TimelineResponse, GetItemTimelineService>();
        _ = services.AddScoped<IValidator<GetQueueRequest>, AcceptAllValidator<GetQueueRequest>>().AddQuery<GetQueueRequest, QueueResponse, GetQueueService>();
        _ = services.AddScoped<IValidator<ExportContextMapRequest>, AcceptAllValidator<ExportContextMapRequest>>().AddQuery<ExportContextMapRequest, MapExported, ExportContextMapService>();
        _ = services.AddScoped<IValidator<GetContextMapRequest>, AcceptAllValidator<GetContextMapRequest>>().AddQuery<GetContextMapRequest, MapStatusView, GetContextMapService>();
        _ = services.AddScoped<IValidator<GetContextSourcesRequest>, AcceptAllValidator<GetContextSourcesRequest>>().AddQuery<GetContextSourcesRequest, SourcesView, GetContextSourcesService>();
        _ = services.AddScoped<IValidator<GetProfileRequest>, AcceptAllValidator<GetProfileRequest>>().AddQuery<GetProfileRequest, GetProfileResponse, GetProfileService>();
        _ = services.AddScoped<IValidator<GetStatusRequest>, AcceptAllValidator<GetStatusRequest>>().AddQuery<GetStatusRequest, GetStatusResponse, GetStatusService>();
        _ = services.AddScoped<IValidator<ListCommandsRequest>, AcceptAllValidator<ListCommandsRequest>>().AddQuery<ListCommandsRequest, ListCommandsResponse, ListCommandsService>();
        _ = services.AddScoped<IValidator<ListItemsRequest>, AcceptAllValidator<ListItemsRequest>>().AddQuery<ListItemsRequest, ListItemsResponse, ListItemsService>();
        _ = services.AddScoped<IValidator<ListServicesRequest>, AcceptAllValidator<ListServicesRequest>>().AddQuery<ListServicesRequest, ListServicesResponse, ListServicesService>();
        _ = services.AddScoped<IValidator<PingProcessRequest>, AcceptAllValidator<PingProcessRequest>>().AddQuery<PingProcessRequest, Ping, PingProcessService>();
        _ = services.AddScoped<IValidator<ShowContextNodeRequest>, AcceptAllValidator<ShowContextNodeRequest>>().AddQuery<ShowContextNodeRequest, NodeView, ShowContextNodeService>();
        _ = services.AddScoped<IValidator<ListContextNeighborsRequest>, AcceptAllValidator<ListContextNeighborsRequest>>().AddQuery<ListContextNeighborsRequest, NeighborsView, ListContextNeighborsService>();
        _ = services.AddScoped<IValidator<FindContextPathRequest>, AcceptAllValidator<FindContextPathRequest>>().AddQuery<FindContextPathRequest, PathView, FindContextPathService>();
        _ = services.AddScoped<IValidator<QueryContextRequest>, AcceptAllValidator<QueryContextRequest>>().AddQuery<QueryContextRequest, ContextResult, QueryContextService>();
        _ = services.AddScoped<IValidator<ReadLedgerRequest>, AcceptAllValidator<ReadLedgerRequest>>().AddQuery<ReadLedgerRequest, LedgerPage, ReadLedgerService>();
        _ = services.AddScoped<IValidator<StreamEventsRequest>, AcceptAllValidator<StreamEventsRequest>>().AddQuery<StreamEventsRequest, StreamEventsResponse, StreamEventsService>();
        return services;
    }

    /// <summary>The context engine's slices (stages S-3 and S-4): the review, promote and proposals commands and the reads of the map, lenses and filemap.</summary>
    private static IServiceCollection AddContextSlices(this IServiceCollection services)
    {
        _ = services
            .AddScoped<ContextScopes>()
            .AddScoped<ContextChallenges>()
            .AddScoped<IScheduledService, ContextDocsRefreshService>()
            .AddScoped<IScheduledService, ContextPurgeService>()
            .AddScoped<IScheduledService, ContextRebuildService>()
            .AddScoped<IScheduledService, ContextSyncService>()
            .AddSingleton<IHeavyJobLock>(static sp => new SindriHeavyJobLock(sp.GetRequiredService<IHeavySlots>(), SindriHeavyJobLock.DefaultWait));
        _ = services.AddScoped<IValidator<PromoteContextMapRequest>, AcceptAllValidator<PromoteContextMapRequest>>().AddCommand<PromoteContextMapRequest, PromoteContextMapResponse, PromoteContextMapService>();
        _ = services.AddScoped<IValidator<ProposeContextMapRequest>, AcceptAllValidator<ProposeContextMapRequest>>().AddCommand<ProposeContextMapRequest, ProposeContextMapResponse, ProposeContextMapService>();
        _ = services.AddScoped<IValidator<ReviewContextMapRequest>, AcceptAllValidator<ReviewContextMapRequest>>().AddCommand<ReviewContextMapRequest, ReviewContextMapResponse, ReviewContextMapService>();
        _ = services.AddScoped<IValidator<ExportContextReviewRequest>, AcceptAllValidator<ExportContextReviewRequest>>().AddQuery<ExportContextReviewRequest, ReviewExported, ExportContextReviewService>();
        _ = services.AddScoped<IValidator<GetContextFileMapRequest>, AcceptAllValidator<GetContextFileMapRequest>>().AddQuery<GetContextFileMapRequest, FileMapView, GetContextFileMapService>();
        _ = services.AddScoped<IValidator<GetContextLensesRequest>, AcceptAllValidator<GetContextLensesRequest>>().AddQuery<GetContextLensesRequest, LensesView, GetContextLensesService>();
        _ = services.AddScoped<IValidator<GetContextEvalMapRequest>, AcceptAllValidator<GetContextEvalMapRequest>>().AddQuery<GetContextEvalMapRequest, MapQualityRun, GetContextEvalMapService>();
        _ = services.AddScoped<IValidator<GetContextProposalsRequest>, AcceptAllValidator<GetContextProposalsRequest>>().AddQuery<GetContextProposalsRequest, ProposalsView, GetContextProposalsService>();
        _ = services.AddScoped<IValidator<GetContextReviewRequest>, AcceptAllValidator<GetContextReviewRequest>>().AddQuery<GetContextReviewRequest, ReviewQueueView, GetContextReviewService>();
        _ = services.AddScoped<IValidator<GetContextReviewHistoryRequest>, AcceptAllValidator<GetContextReviewHistoryRequest>>().AddQuery<GetContextReviewHistoryRequest, ReviewHistoryView, GetContextReviewHistoryService>();
        return services;
    }

    /// <summary>The engine's instance slices (engine P4): start, command and complete (commands), the nonce (an issue), get, list and events (queries).</summary>
    private static IServiceCollection AddEngineSlices(this IServiceCollection services)
    {
        _ = services.AddScoped<IValidator<StartInstanceRequest>, AcceptAllValidator<StartInstanceRequest>>().AddCommand<StartInstanceRequest, StartInstanceResponse, StartInstanceService>();
        _ = services.AddScoped<StartFacts>().AddScoped<ClaimHolders>().AddScoped<EngineFilledReport>();   // engine P6 unit (d): SND-WF-012 against workflow_claims
        _ = services.AddScoped<IValidator<ApplyCommandRequest>, AcceptAllValidator<ApplyCommandRequest>>().AddCommand<ApplyCommandRequest, ApplyCommandResponse, ApplyCommandService>();
        _ = services.AddScoped<IValidator<CompleteStepRequest>, AcceptAllValidator<CompleteStepRequest>>().AddCommand<CompleteStepRequest, CompleteStepResponse, CompleteStepService>();
        _ = services.AddScoped<IValidator<IssueNonceRequest>, AcceptAllValidator<IssueNonceRequest>>().AddIssue<IssueNonceRequest, IssueNonceResponse, IssueNonceService>();
        _ = services.AddScoped<IValidator<GetInstanceRequest>, AcceptAllValidator<GetInstanceRequest>>().AddQuery<GetInstanceRequest, InstanceDetail, GetInstanceService>();
        _ = services.AddScoped<IValidator<ListInstancesRequest>, AcceptAllValidator<ListInstancesRequest>>().AddQuery<ListInstancesRequest, InstanceList, ListInstancesService>();
        _ = services.AddScoped<IValidator<GetInstanceEventsRequest>, AcceptAllValidator<GetInstanceEventsRequest>>().AddQuery<GetInstanceEventsRequest, GetInstanceEventsResponse, GetInstanceEventsService>();
        return services;
    }

    /// <summary>The doctor: its fact readers, the core checks and the extra checks decorator.</summary>
    private static IServiceCollection AddDoctor(this IServiceCollection services) => services
        .AddScoped<ProfileFacts>()
        .AddScoped<LedgerFacts>()
        .AddScoped<OnyxDoctor>()
        .AddScoped<ContextEngineChecks>()
        .AddScoped<RunDoctorService>()
        .AddScoped<IService<RunDoctorRequest, RunDoctorResponse>>(static sp =>
            new WithMoreChecks(sp.GetRequiredService<RunDoctorService>(), sp.GetServices<IDoctorChecks>()))
        .AddScoped<IDoctorChecks, ContextDoctorChecks>();

    /// <summary>Onyx's status (P1): read-only and in process, like the doctor, but its own slice.</summary>
    private static IServiceCollection AddOnyxStatus(this IServiceCollection services) => services
        .AddScoped<IService<OnyxStatusRequest, OnyxStatusResponse>, OnyxStatusService>();

    /// <summary>The registry's slices (engine P2): register, approve and revoke (commands), the challenge (an issue), list and show (queries).</summary>
    private static IServiceCollection AddRegistrySlices(this IServiceCollection services)
    {
        _ = services.AddScoped<IValidator<RegisterRecipeRequest>, AcceptAllValidator<RegisterRecipeRequest>>().AddCommand<RegisterRecipeRequest, RegisterRecipeResponse, RegisterRecipeService>();
        _ = services.AddScoped<IValidator<ApproveRecipeRequest>, AcceptAllValidator<ApproveRecipeRequest>>().AddCommand<ApproveRecipeRequest, ApproveRecipeResponse, ApproveRecipeService>();
        _ = services.AddScoped<IValidator<RevokeRecipeRequest>, AcceptAllValidator<RevokeRecipeRequest>>().AddCommand<RevokeRecipeRequest, RevokeRecipeResponse, RevokeRecipeService>();
        _ = services.AddScoped<IValidator<IssueChallengeRequest>, AcceptAllValidator<IssueChallengeRequest>>().AddIssue<IssueChallengeRequest, IssueChallengeResponse, IssueChallengeService>();
        _ = services.AddScoped<IValidator<ListRecipesRequest>, AcceptAllValidator<ListRecipesRequest>>().AddQuery<ListRecipesRequest, ListRecipesResponse, ListRecipesService>();
        _ = services.AddScoped<IValidator<ShowRecipeRequest>, AcceptAllValidator<ShowRecipeRequest>>().AddQuery<ShowRecipeRequest, RecipeView, ShowRecipeService>();
        _ = services.AddScoped<IValidator<GetInstanceFactRequest>, AcceptAllValidator<GetInstanceFactRequest>>().AddQuery<GetInstanceFactRequest, FactView, GetInstanceFactService>();
        _ = services.AddScoped<IValidator<ListOutboxRequest>, AcceptAllValidator<ListOutboxRequest>>().AddQuery<ListOutboxRequest, ListOutboxResponse, ListOutboxService>();   // engine P6 unit (c)
        return services;
    }
}
