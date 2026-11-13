using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.AgentNodeControl;
using Aw.Sindri.Features.AnswerQuestion;
using Aw.Sindri.Features.ApplyWorkflowOps;
using Aw.Sindri.Features.CancelJob;
using Aw.Sindri.Features.CancelWorkflowRun;
using Aw.Sindri.Features.CheckProfile;
using Aw.Sindri.Features.CloseItem;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.CreateHandoffItem;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Features.Decide;
using Aw.Sindri.Features.DeleteWorkflow;
using Aw.Sindri.Features.FileRequest;
using Aw.Sindri.Features.GetEventCatalog;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.GetItemTimeline;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Features.GetLaunch;
using Aw.Sindri.Features.GetLaunchTargets;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Features.GetProfile;
using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Features.GetRepoStatus;
using Aw.Sindri.Features.GetRunSuspension;
using Aw.Sindri.Features.GetSession;
using Aw.Sindri.Features.GetSessionPreview;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.GetWorkflow;
using Aw.Sindri.Features.GetWorkflowRun;
using Aw.Sindri.Features.InitProfile;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Features.JobProbe;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Features.Launching;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Features.ListDecisions;
using Aw.Sindri.Features.ListItems;
using Aw.Sindri.Features.ListJobs;
using Aw.Sindri.Features.ListRequests;
using Aw.Sindri.Features.ListResources;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Features.ListSessions;
using Aw.Sindri.Features.ListTriggerEvents;
using Aw.Sindri.Features.ListWorkflowBlocks;
using Aw.Sindri.Features.ListWorkflowRuns;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Features.ListWorkflowTemplates;
using Aw.Sindri.Features.OpenItem;
using Aw.Sindri.Features.PauseService;
using Aw.Sindri.Features.PauseWorkflow;
using Aw.Sindri.Features.PingProcess;
using Aw.Sindri.Features.PinWorkflowOutput;
using Aw.Sindri.Features.PurgeResources;
using Aw.Sindri.Features.ReadLedger;
using Aw.Sindri.Features.RebuildViews;
using Aw.Sindri.Features.RecordHeartbeat;
using Aw.Sindri.Features.RegisterMcp;
using Aw.Sindri.Features.ReplaceWorkflow;
using Aw.Sindri.Features.ResumeJob;
using Aw.Sindri.Features.ResumeService;
using Aw.Sindri.Features.RetireInstancesOnStart;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.RunWorkflow;
using Aw.Sindri.Features.SampleSessionUsage;
using Aw.Sindri.Features.Scrub;
using Aw.Sindri.Features.SearchWorkflowDocs;
using Aw.Sindri.Features.SessionExit;
using Aw.Sindri.Features.SessionHeartbeat;
using Aw.Sindri.Features.Sessions;
using Aw.Sindri.Features.SetProfileKey;
using Aw.Sindri.Features.SetupWatch;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Features.SweepLostSessions;
using Aw.Sindri.Features.TestWorkflow;
using Aw.Sindri.Features.ValidateProfile;
using Aw.Sindri.Features.ValidateWorkflow;
using Aw.Sindri.Features.WorkflowSchedule;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
            .AddWorkflowServices()
            .AddScoped<IValidator<AnswerQuestionRequest>, AcceptAllValidator<AnswerQuestionRequest>>().AddCommand<AnswerQuestionRequest, AnswerQuestionResponse, AnswerQuestionService>()   // 'sindri answer' (#2258, PR 9)
            .AddScoped<IValidator<ListResourcesRequest>, AcceptAllValidator<ListResourcesRequest>>().AddQuery<ListResourcesRequest, ListResourcesResponse, ListResourcesService>()   // agent-first PR 14: the owner is parsed in the service (SND-RES-006)
            .AddScoped<IValidator<PurgeResourcesRequest>, AcceptAllValidator<PurgeResourcesRequest>>().AddCommand<PurgeResourcesRequest, PurgeResourcesResponse, PurgeResourcesService>()
            .AddScoped<IService<RecordHeartbeatRequest, RecordHeartbeatResponse>, RecordHeartbeatService>()
            .AddScoped<IService<RetireInstancesOnStartRequest, RetireInstancesOnStartResponse>, RetireInstancesOnStartService>()
            .AddRecordServiceRun()
            .AddScoped<IService<CheckProfileRequest, CheckProfileResponse>, CheckProfileService>()
            .AddScoped<IService<ValidateProfileRequest, ValidateProfileResponse>, ValidateProfileService>()
            .AddPlanSandbox().AddRecordLimitHit().AddEndLearnMode().AddListDenials().AddAllowDenial().AddCheckSandbox().AddWorkspacePrepare().AddGetSkills()
            .AddScoped<IService<InitProfileRequest, InitProfileResponse>, InitProfileService>()
            .AddScoped<IService<SetProfileKeyRequest, SetProfileKeyResponse>, SetProfileKeyService>()
            .AddScoped<IService<ScrubRequest, ScrubResponse>, ScrubService>()
            .AddDoctor()
            .AddScoped<ServeRecords>()
            .AddScoped<IService<RegisterMcpRequest, RegisterMcpResponse>, RegisterMcpService>()
            .AddScoped<IService<GetRepoStatusRequest, GetRepoStatusResponse>, GetRepoStatusService>()
            .AddScoped<IScheduledService, HeartbeatService>()
            .AddSetupWatch(static sp => sp.GetRequiredService<IJobRunner>().Running)
            .AddScoped<IScheduledService, JobProbeService>()   // opt-in: not a BuiltInService, so only a profile that names it schedules it
            .AddJobRuntime()
            .AddContextSlices().AddCodeIndexReads().AddEngineReads()
            .AddContextJobs()
            .AddCodeIndexJobs()
            .AddEngineSlices()
            .AddSessionSlices().AddTerminalSocket()
            .AddLaunchSlices()
            .AddLaunchTargetJob()
            .AddAddRepo().AddToolkits().AddPinConcepts().AddSetPages().AddRegisterMcpJob().AddOnboardRepo();
        return services;
    }

    private static IServiceCollection AddWorkflowServices(this IServiceCollection services)
    {
        _ = services
            .AddScoped<IScheduledService, WorkflowScheduleService>().AddSingleton(WorkflowScheduleService.BuiltIn)   // the built-in 'workflow-schedule' service (#2258, PR 6): runs without a profile entry, every minute
            .AddScoped<IValidator<RunWorkflowRequest>, AcceptAllValidator<RunWorkflowRequest>>().AddCommand<RunWorkflowRequest, RunWorkflowResponse, RunWorkflowService>().AddScoped<ISetupWorkflowRunner, SetupWorkflowRunner>()   // 'sindri wf run' and 'wf cancel' (#2258, PR 6): behind the command chain; the slug and payload are checked by the service
            .AddScoped<IValidator<CancelWorkflowRunRequest>, AcceptAllValidator<CancelWorkflowRunRequest>>().AddCommand<CancelWorkflowRunRequest, CancelWorkflowRunResponse, CancelWorkflowRunService>()
            .AddScoped<IValidator<TakeOverAgentNodeRequest>, AcceptAllValidator<TakeOverAgentNodeRequest>>().AddCommand<TakeOverAgentNodeRequest, AgentNodeControlled, TakeOverAgentNodeService>()   // the human takes a run_agent node over and hands it back (#2472): behind the command chain; the services check the caller, the run and the node
            .AddScoped<IValidator<HandBackAgentNodeRequest>, AcceptAllValidator<HandBackAgentNodeRequest>>().AddCommand<HandBackAgentNodeRequest, AgentNodeControlled, HandBackAgentNodeService>()
            .AddScoped<IValidator<ListWorkflowsRequest>, AcceptAllValidator<ListWorkflowsRequest>>().AddQuery<ListWorkflowsRequest, ListWorkflowsResponse, ListWorkflowsService>()   // the workflow reads (#2258, PR 8a): 'sindri wf list|show|runs|run-show'; the slug and the run id are typed, the services say what is missing
            .AddScoped<IValidator<GetWorkflowRequest>, AcceptAllValidator<GetWorkflowRequest>>().AddQuery<GetWorkflowRequest, GetWorkflowResponse, GetWorkflowService>()
            .AddScoped<IValidator<ListWorkflowRunsRequest>, AcceptAllValidator<ListWorkflowRunsRequest>>().AddQuery<ListWorkflowRunsRequest, ListWorkflowRunsResponse, ListWorkflowRunsService>()
            .AddScoped<IValidator<GetWorkflowRunRequest>, AcceptAllValidator<GetWorkflowRunRequest>>().AddQuery<GetWorkflowRunRequest, GetWorkflowRunResponse, GetWorkflowRunService>()   // PR 8b below: the workflow writes
            .AddScoped<IValidator<CreateWorkflowRequest>, AcceptAllValidator<CreateWorkflowRequest>>().AddCommand<CreateWorkflowRequest, CreateWorkflowResponse, CreateWorkflowService>().AddScoped<ISetupWorkflowCreator, SetupWorkflowCreator>()
            .AddScoped<IValidator<ApplyWorkflowOpsRequest>, AcceptAllValidator<ApplyWorkflowOpsRequest>>().AddCommand<ApplyWorkflowOpsRequest, ApplyWorkflowOpsResponse, ApplyWorkflowOpsService>()
            .AddScoped<IValidator<ReplaceWorkflowRequest>, AcceptAllValidator<ReplaceWorkflowRequest>>().AddCommand<ReplaceWorkflowRequest, ReplaceWorkflowResponse, ReplaceWorkflowService>()
            .AddScoped<IValidator<ValidateWorkflowRequest>, AcceptAllValidator<ValidateWorkflowRequest>>().AddCommand<ValidateWorkflowRequest, ValidateWorkflowResponse, ValidateWorkflowService>()
            .AddScoped<IValidator<ActivateWorkflowRequest>, AcceptAllValidator<ActivateWorkflowRequest>>().AddCommand<ActivateWorkflowRequest, ActivateWorkflowResponse, ActivateWorkflowService>().AddScoped<ISetupWorkflowActivator, SetupWorkflowActivator>()
            .AddScoped<IValidator<PauseWorkflowRequest>, AcceptAllValidator<PauseWorkflowRequest>>().AddCommand<PauseWorkflowRequest, PauseWorkflowResponse, PauseWorkflowService>()
            .AddScoped<IValidator<DeleteWorkflowRequest>, AcceptAllValidator<DeleteWorkflowRequest>>().AddCommand<DeleteWorkflowRequest, DeleteWorkflowResponse, DeleteWorkflowService>()
            .AddScoped<IValidator<ListWorkflowBlocksRequest>, AcceptAllValidator<ListWorkflowBlocksRequest>>().AddQuery<ListWorkflowBlocksRequest, ListWorkflowBlocksResponse, ListWorkflowBlocksService>()   // 'wf blocks' and 'wf docs'
            .AddScoped<IValidator<SearchWorkflowDocsRequest>, AcceptAllValidator<SearchWorkflowDocsRequest>>().AddQuery<SearchWorkflowDocsRequest, SearchWorkflowDocsResponse, SearchWorkflowDocsService>()
            .AddScoped<IValidator<TestWorkflowRequest>, AcceptAllValidator<TestWorkflowRequest>>().AddCommand<TestWorkflowRequest, TestWorkflowResponse, TestWorkflowService>()   // the Workflows page P0 contract stubs (#3191): each refuses with SND-GRAPH-119 until its phase builds it
            .AddScoped<IValidator<PinWorkflowOutputRequest>, AcceptAllValidator<PinWorkflowOutputRequest>>().AddCommand<PinWorkflowOutputRequest, PinWorkflowOutputResponse, PinWorkflowOutputService>()
            .AddScoped<IValidator<ListTriggerEventsRequest>, AcceptAllValidator<ListTriggerEventsRequest>>().AddQuery<ListTriggerEventsRequest, ListTriggerEventsResponse, ListTriggerEventsService>()
            .AddScoped<IValidator<ListWorkflowTemplatesRequest>, AcceptAllValidator<ListWorkflowTemplatesRequest>>().AddQuery<ListWorkflowTemplatesRequest, ListWorkflowTemplatesResponse, ListWorkflowTemplatesService>();
        return services;
    }

    private static IServiceCollection AddCommands(this IServiceCollection services)
    {
        // ---- commands (one line per slice) ----
        _ = services.AddScoped<IValidator<CancelJobRequest>, AcceptAllValidator<CancelJobRequest>>().AddCommand<CancelJobRequest, CommandAccepted, CancelJobService>();
        _ = services.AddScoped<IValidator<PauseServiceRequest>, AcceptAllValidator<PauseServiceRequest>>().AddCommand<PauseServiceRequest, CommandAccepted, PauseServiceService>();
        _ = services.AddScoped<IValidator<ResumeServiceRequest>, AcceptAllValidator<ResumeServiceRequest>>().AddCommand<ResumeServiceRequest, CommandAccepted, ResumeServiceService>();
        _ = services.AddScoped<IValidator<ResumeJobRequest>, AcceptAllValidator<ResumeJobRequest>>().AddCommand<ResumeJobRequest, JobStartedResponse, ResumeJobService>();
        _ = services.AddStartSession();
        _ = services.AddRunService();
        _ = services.AddScoped<IValidator<CloseItemRequest>, CloseItemValidator>().AddCommand<CloseItemRequest, CommandAccepted, CloseItemService>();
        services.TryAddSingleton<IFixTaskFiles, NoFixTaskFiles>();   // the fix branch's file-backed adapter replaces it
        _ = services.AddScoped<IFixItemCloser, FixItemCloser>();
        _ = services.AddScoped<IValidator<OpenItemRequest>, OpenItemValidator>().AddCommand<OpenItemRequest, LaunchRequested, OpenItemService>().AddScoped<OpenChecks>().AddSingleton<OpenRequests>().AddScoped<OpenTarget>();
        _ = services.AddScoped<ISessionUsageSampler, SessionUsageSampler>();   // API P10a U11: the 60 s sample of what a live session has used
        _ = services.AddScoped<IIdleSessions, IdleSessions>().AddScoped<IdleReads>().AddScoped<StepTakeovers>().AddScoped<IStalledAgentRuns, StalledAgentRuns>();   // a Step session whose agent ended its turn with no report
        _ = services.AddScoped<ILostSessions, LostSessions>()   // API P10a U10: 'session wrap' says the session is alive and when it exited (issues, proven by the launch secret)
            .AddScoped<IValidator<SessionHeartbeatRequest>, AcceptAllValidator<SessionHeartbeatRequest>>().AddIssue<SessionHeartbeatRequest, SessionHeartbeatResponse, SessionHeartbeatService>().AddScoped<IValidator<SessionExitRequest>, AcceptAllValidator<SessionExitRequest>>().AddIssue<SessionExitRequest, SessionExitResponse, SessionExitService>();
        _ = services.AddScoped<IValidator<DecideRequest>, DecideValidator>().AddCommand<DecideRequest, CommandAccepted, DecideService>();
        _ = services.AddScoped<IValidator<FileRequestRequest>, FileRequestValidator>().AddCommand<FileRequestRequest, RequestFiledResponse, FileRequestService>();
        _ = services.AddProjectCommands().AddConnectionSlices().AddMemorySlices().AddToolkitCallSlices().AddTodoSlices().AddLinearReads().AddLinearConnection().AddReviewCommands().AddScoped<IValidator<CreateItemRequest>, CreateItemValidator>().AddCommand<CreateItemRequest, ItemCreatedResponse, CreateItemService>().AddScoped<IItemMaker, FixItemMaker>().AddScoped<IValidator<CreateHandoffItemRequest>, CreateHandoffItemValidator>().AddCommand<CreateHandoffItemRequest, HandoffItemCreatedResponse, CreateHandoffItemService>().AddScoped<WorkflowItemMaker>().AddScoped<RunItemLinker>().AddScoped<RunItemRepair>().AddRunItemRepair()
            .AddSingleton<ICreateItemPort>(static sp => new ScopedCreateItemPort(async (item, ct) =>
            {
                var scope = sp.CreateAsyncScope();   // why: the engine's blocks are singletons, the create-item handler is scoped
                await using (scope.ConfigureAwait(false))
                {
                    return await scope.ServiceProvider.GetRequiredService<WorkflowItemMaker>().Create(item, ct).ConfigureAwait(false);
                }
            }));
        _ = services.AddScoped<IValidator<RebuildViewsRequest>, AcceptAllValidator<RebuildViewsRequest>>().AddCommand<RebuildViewsRequest, CommandAccepted, RebuildViewsService>();
        _ = services.AddScoped<IValidator<ImportContextMapRequest>, AcceptAllValidator<ImportContextMapRequest>>().AddCommand<ImportContextMapRequest, ImportContextMapResponse, ImportContextMapService>();
        _ = services.AddScoped<Core.Profile.ModelKeyChecks>();
        _ = services.AddScoped<IValidator<StopProcessRequest>, AcceptAllValidator<StopProcessRequest>>().AddStopCommand<StopProcessRequest, StopProcessResponse, StopProcessService>();   // never behind the gate: a stop cancels its holder
        return services;
    }

    private static IServiceCollection AddQueries(this IServiceCollection services)
    {
        // ---- queries (one line per slice) ----
        _ = services.AddScoped<IValidator<GetEventCatalogRequest>, AcceptAllValidator<GetEventCatalogRequest>>().AddQuery<GetEventCatalogRequest, EventCatalogView, GetEventCatalogService>();
        _ = services.AddScoped<IValidator<GetNeedsYouRequest>, AcceptAllValidator<GetNeedsYouRequest>>().AddQuery<GetNeedsYouRequest, GetNeedsYouResponse, GetNeedsYouService>().AddScoped<NeedsYouLiveRows>().AddSingleton<TurnOnMcpSighting>();
        _ = services.AddScoped<IValidator<ListDecisionsRequest>, AcceptAllValidator<ListDecisionsRequest>>().AddQuery<ListDecisionsRequest, ListDecisionsResponse, ListDecisionsService>();
        _ = services.AddScoped<IValidator<ListRequestsRequest>, AcceptAllValidator<ListRequestsRequest>>().AddQuery<ListRequestsRequest, ListRequestsResponse, ListRequestsService>();
        _ = services.AddScoped<IValidator<GetJobRequest>, AcceptAllValidator<GetJobRequest>>().AddQuery<GetJobRequest, JobView, GetJobService>();
        _ = services.AddScoped<IValidator<GetLaunchRequest>, AcceptAllValidator<GetLaunchRequest>>().AddQuery<GetLaunchRequest, LaunchView, GetLaunchService>().AddScoped<IValidator<GetRunSuspensionRequest>, AcceptAllValidator<GetRunSuspensionRequest>>().AddQuery<GetRunSuspensionRequest, RunSuspensionView, GetRunSuspensionService>();
        _ = services.AddScoped<IValidator<GetLaunchTargetsRequest>, AcceptAllValidator<GetLaunchTargetsRequest>>().AddQuery<GetLaunchTargetsRequest, LaunchTargets, GetLaunchTargetsService>();
        _ = services.AddScoped<IValidator<ListJobsRequest>, AcceptAllValidator<ListJobsRequest>>().AddQuery<ListJobsRequest, JobList, ListJobsService>();
        _ = services.AddScoped<IValidator<GetItemRequest>, AcceptAllValidator<GetItemRequest>>().AddQuery<GetItemRequest, ItemView, GetItemService>().AddScoped<LaunchTargetsReader>().AddScoped<IItemLaunchSource, ItemLaunchSource>();
        _ = services.AddScoped<IValidator<GetItemTimelineRequest>, AcceptAllValidator<GetItemTimelineRequest>>().AddQuery<GetItemTimelineRequest, TimelineResponse, GetItemTimelineService>();
        _ = services.AddScoped<IValidator<GetQueueRequest>, AcceptAllValidator<GetQueueRequest>>().AddQuery<GetQueueRequest, QueueResponse, GetQueueService>();
        _ = services.AddScoped<IValidator<ExportContextMapRequest>, AcceptAllValidator<ExportContextMapRequest>>().AddQuery<ExportContextMapRequest, MapExported, ExportContextMapService>();
        _ = services.AddScoped<IValidator<GetContextMapRequest>, AcceptAllValidator<GetContextMapRequest>>().AddQuery<GetContextMapRequest, MapStatusView, GetContextMapService>();
        _ = services.AddScoped<IValidator<GetMapPublishStatusRequest>, AcceptAllValidator<GetMapPublishStatusRequest>>().AddQuery<GetMapPublishStatusRequest, MapPublishStatusView, GetMapPublishStatusService>().AddScoped<MapPublishStatusReader>().AddScoped<IMapPublishCheck, MapPublishDoctorCheck>();
        _ = services.AddScoped<IValidator<GetContextSourcesRequest>, AcceptAllValidator<GetContextSourcesRequest>>().AddQuery<GetContextSourcesRequest, SourcesView, GetContextSourcesService>();
        _ = services.AddScoped<IValidator<GetProfileRequest>, AcceptAllValidator<GetProfileRequest>>().AddQuery<GetProfileRequest, GetProfileResponse, GetProfileService>();
        _ = services.AddReviewQueries();
        _ = services.AddLinearQueries().AddLinearWrites().AddLinearCommentCreate();
        _ = services.AddScoped<ProfileReads>().AddScoped<HealthServiceRows>();
        _ = services.AddGetSetup().AddAppConfirm().AddFirstRun();
        _ = services.AddScoped<IValidator<GetStatusRequest>, AcceptAllValidator<GetStatusRequest>>().AddQuery<GetStatusRequest, GetStatusResponse, GetStatusService>();
        _ = services.AddScoped<IValidator<ListCommandsRequest>, AcceptAllValidator<ListCommandsRequest>>().AddQuery<ListCommandsRequest, ListCommandsResponse, ListCommandsService>();
        _ = services.AddScoped<IValidator<ListItemsRequest>, AcceptAllValidator<ListItemsRequest>>().AddQuery<ListItemsRequest, ListItemsResponse, ListItemsService>();
        _ = services.AddScoped<IValidator<ListServicesRequest>, AcceptAllValidator<ListServicesRequest>>().AddQuery<ListServicesRequest, ListServicesResponse, ListServicesService>();
        _ = services.AddScoped<IValidator<ListSessionsRequest>, AcceptAllValidator<ListSessionsRequest>>().AddQuery<ListSessionsRequest, SessionList, ListSessionsService>();
        _ = services.AddScoped<IValidator<GetSessionRequest>, AcceptAllValidator<GetSessionRequest>>().AddQuery<GetSessionRequest, SessionView, GetSessionService>();
        _ = services.AddSingleton<PaneCaptures>();
        _ = services.AddScoped<IValidator<GetSessionPreviewRequest>, AcceptAllValidator<GetSessionPreviewRequest>>().AddQuery<GetSessionPreviewRequest, GetSessionPreviewResponse, GetSessionPreviewService>();
        _ = services.AddScoped<IValidator<PingProcessRequest>, AcceptAllValidator<PingProcessRequest>>().AddQuery<PingProcessRequest, Ping, PingProcessService>();
        _ = services.AddScoped<IValidator<ShowContextNodeRequest>, AcceptAllValidator<ShowContextNodeRequest>>().AddQuery<ShowContextNodeRequest, NodeView, ShowContextNodeService>();
        _ = services.AddScoped<IValidator<ListContextNeighborsRequest>, AcceptAllValidator<ListContextNeighborsRequest>>().AddQuery<ListContextNeighborsRequest, NeighborsView, ListContextNeighborsService>();
        _ = services.AddScoped<IValidator<FindContextPathRequest>, AcceptAllValidator<FindContextPathRequest>>().AddQuery<FindContextPathRequest, PathView, FindContextPathService>();
        _ = services.AddScoped<IValidator<QueryContextRequest>, AcceptAllValidator<QueryContextRequest>>().AddQuery<QueryContextRequest, ContextResult, QueryContextService>();
        _ = services.AddScoped<IValidator<ReadLedgerRequest>, AcceptAllValidator<ReadLedgerRequest>>().AddQuery<ReadLedgerRequest, LedgerPage, ReadLedgerService>();
        _ = services.AddScoped<IValidator<StreamEventsRequest>, AcceptAllValidator<StreamEventsRequest>>().AddQuery<StreamEventsRequest, StreamEventsResponse, StreamEventsService>();
        return services;
    }

    /// <summary>The report of a launched agent run; refuses with SND-GRAPH-104 until the graph engine's agent block (PR 7).</summary>
    private static IServiceCollection AddEngineSlices(this IServiceCollection services)
    {
        _ = services.AddScoped<IValidator<CompleteStepRequest>, AcceptAllValidator<CompleteStepRequest>>().AddCommand<CompleteStepRequest, CompleteStepResponse, CompleteStepService>();
        _ = services.AddScoped<IValidator<IssueChallengeRequest>, AcceptAllValidator<IssueChallengeRequest>>().AddIssue<IssueChallengeRequest, IssueChallengeResponse, IssueChallengeService>();   // refuses every action until #2373 removes the route
        return services;
    }

    /// <summary>The doctor: its fact readers, the core checks and the extra checks decorator.</summary>
    private static IServiceCollection AddDoctor(this IServiceCollection services) => services
        .AddScoped<ProfileFacts>()
        .AddScoped<LedgerFacts>().AddScoped<RuntimeFacts>()
        .AddScoped<LinearDoctor>()
        .AddScoped<ContextEngineChecks>()
        .AddScoped<RunDoctorService>()
        .AddSingleton<IPartTimer, LoggingPartTimer>()
        .AddScoped<IService<RunDoctorRequest, RunDoctorResponse>>(static sp =>
            new WithMoreChecks(sp.GetRequiredService<RunDoctorService>(), sp.GetServices<IDoctorChecks>(), sp.GetRequiredService<ILoggerFactory>().CreateLogger("Aw.Sindri.Doctor")))
        .AddScoped<IDoctorChecks, ContextDoctorChecks>().AddIndexDoctor()   // AddIndexDoctor: owned index (#1394), context:index-*
        .AddScoped<IDoctorChecks, LauncherDoctorChecks>().AddScoped<IDoctorChecks, AgentTargetDoctor>()   // #1428: `agent:target`
        .AddScoped<IDoctorChecks, SyncTodoNote.TodoNoteDoctorChecks>()                    // the Obsidian note's sync (spec §6): the `todo` row
        .AddScoped<IDoctorChecks, PollConnections.CatchUpDoctorChecks>()                   // #2437: `connections:catchup`, the last capped or gapped read per connection and kind
        .AddScoped<IDoctorChecks, SetupDoctor>()                                          // SO-A6: `setup`, ok when every step is done or skipped
        .AddScoped<IDoctorChecks, Leftovers.OnyxLeftoverDoctor>();                                // #1598: `index:onyx-leftover`, only while an old `onyx up` left something

    /// <summary>The engine's singleton <see cref="IRunItemRepair"/>: each call runs the scoped <see cref="RunItemRepair"/> in its own scope (#2588).</summary>
    private static IServiceCollection AddRunItemRepair(this IServiceCollection services) => services
        .AddSingleton<IRunItemRepair>(static sp =>
        {
            var scopes = sp.GetRequiredService<IServiceScopeFactory>();
            return new ScopedRunItemRepair(async work =>
            {
                var scope = scopes.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    return await work(scope.ServiceProvider.GetRequiredService<RunItemRepair>()).ConfigureAwait(false);
                }
            });
        });
}
