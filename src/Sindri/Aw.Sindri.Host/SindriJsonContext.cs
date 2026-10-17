using System.Text.Json.Serialization;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.ApplyCommand;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.CancelJob;
using Aw.Sindri.Features.CloseItem;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.Decide;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.FileRequest;
using Aw.Sindri.Features.GetEventCatalog;
using Aw.Sindri.Features.GetInstanceEvents;
using Aw.Sindri.Features.GetInstanceFact;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.GetItemTimeline;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Features.GetLaunch;
using Aw.Sindri.Features.GetLaunchTargets;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Features.GetProfile;
using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.IssueNonce;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Features.ListDecisions;
using Aw.Sindri.Features.ListItems;
using Aw.Sindri.Features.ListJobs;
using Aw.Sindri.Features.ListOutbox;
using Aw.Sindri.Features.ListRecipes;
using Aw.Sindri.Features.ListRequests;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Features.OpenItem;
using Aw.Sindri.Features.PauseService;
using Aw.Sindri.Features.ProbeLaunchTargets;
using Aw.Sindri.Features.ReadLedger;
using Aw.Sindri.Features.RebuildViews;
using Aw.Sindri.Features.RegisterMcp;
using Aw.Sindri.Features.RegisterRecipe;
using Aw.Sindri.Features.ResumeService;
using Aw.Sindri.Features.RevokeRecipe;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.RunService;
using Aw.Sindri.Features.SessionExit;
using Aw.Sindri.Features.SessionHeartbeat;
using Aw.Sindri.Features.StartInstance;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Features.ValidateProfile;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Host.Commands.Probe;
using Aw.Sindri.Host.Commands.Recipe;
using Aw.Sindri.Host.Endpoints;
using Aw.Sindri.Host.Mcp;

namespace Aw.Sindri.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(AgentLimits))]
[JsonSerializable(typeof(Error)), JsonSerializable(typeof(SindriProfile)), JsonSerializable(typeof(ControlEndpoint)), JsonSerializable(typeof(Ping))]
[JsonSerializable(typeof(ApiProblem)), JsonSerializable(typeof(ProcessStarted)), JsonSerializable(typeof(ProcessStopped)), JsonSerializable(typeof(WorkflowEventPayload))]
[JsonSerializable(typeof(ProfileApproved)), JsonSerializable(typeof(Heartbeat)), JsonSerializable(typeof(CommandHandled)), JsonSerializable(typeof(ServiceRan))]
[JsonSerializable(typeof(ProfileChecked)), JsonSerializable(typeof(ViewsRebuilt)), JsonSerializable(typeof(ItemCreated)), JsonSerializable(typeof(ItemCreatedPublic))]
[JsonSerializable(typeof(ItemStateChanged)), JsonSerializable(typeof(ItemLinked)), JsonSerializable(typeof(RequestFiled)), JsonSerializable(typeof(RequestFiledPublic))]
[JsonSerializable(typeof(DecisionFact)), JsonSerializable(typeof(DecisionRecorded)), JsonSerializable(typeof(DecisionRecordedPublic)), JsonSerializable(typeof(JobStarted))]
[JsonSerializable(typeof(JobProgress)), JsonSerializable(typeof(JobFinished)), JsonSerializable(typeof(JobFinishedPublic)), JsonSerializable(typeof(JobCancelled))]
[JsonSerializable(typeof(ServicePaused)), JsonSerializable(typeof(ServiceResumed)), JsonSerializable(typeof(JobRow)), JsonSerializable(typeof(JobLine))]
[JsonSerializable(typeof(JobLinks)), JsonSerializable(typeof(JobStartedResponse)), JsonSerializable(typeof(RunServiceRequest)), JsonSerializable(typeof(PauseServiceRequest))]
[JsonSerializable(typeof(ResumeServiceRequest)), JsonSerializable(typeof(CancelJobRequest)), JsonSerializable(typeof(GetJobRequest)), JsonSerializable(typeof(JobView))]
[JsonSerializable(typeof(ListJobsRequest)), JsonSerializable(typeof(JobList)), JsonSerializable(typeof(JobSummary)), JsonSerializable(typeof(ScheduledResult))]
[JsonSerializable(typeof(AsOf)), JsonSerializable(typeof(ViewsStatus)), JsonSerializable(typeof(ServiceRow)), JsonSerializable(typeof(ProfileRow))]
[JsonSerializable(typeof(ItemRow)), JsonSerializable(typeof(AliasRow)), JsonSerializable(typeof(RequestRow)), JsonSerializable(typeof(HumanDecisionRow))]
[JsonSerializable(typeof(NeedsYouRowRecord)), JsonSerializable(typeof(NeedsYouRow)), JsonSerializable(typeof(NeedsYouSubject)), JsonSerializable(typeof(TimelineRow))]
[JsonSerializable(typeof(HealthReport)), JsonSerializable(typeof(CommandAccepted)), JsonSerializable(typeof(GetStatusResponse)), JsonSerializable(typeof(StopProcessResponse))]
[JsonSerializable(typeof(ApproveProfileRequest)), JsonSerializable(typeof(ApproveProfileResponse)), JsonSerializable(typeof(ValidateProfileResponse)), JsonSerializable(typeof(RunDoctorResponse))]
[JsonSerializable(typeof(PublicEvent)), JsonSerializable(typeof(LedgerPage)), JsonSerializable(typeof(EventCatalogView)), JsonSerializable(typeof(EventTypeView))]
[JsonSerializable(typeof(StreamErrorFrame)), JsonSerializable(typeof(ListCommandsRequest)), JsonSerializable(typeof(ListCommandsResponse)), JsonSerializable(typeof(CommandListRow))]
[JsonSerializable(typeof(ListServicesRequest)), JsonSerializable(typeof(ListServicesResponse)), JsonSerializable(typeof(ServiceView)), JsonSerializable(typeof(GetProfileRequest))]
[JsonSerializable(typeof(GetProfileResponse)), JsonSerializable(typeof(RebuildViewsRequest)), JsonSerializable(typeof(CreateItemRequest)), JsonSerializable(typeof(ItemCreatedResponse))]
[JsonSerializable(typeof(CloseItemRequest)), JsonSerializable(typeof(CloseItemBody)), JsonSerializable(typeof(GetQueueRequest)), JsonSerializable(typeof(QueueResponse))]
[JsonSerializable(typeof(QueueGroupView)), JsonSerializable(typeof(ItemSummary)), JsonSerializable(typeof(ListItemsRequest)), JsonSerializable(typeof(ListItemsResponse))]
[JsonSerializable(typeof(GetItemRequest)), JsonSerializable(typeof(ItemView)), JsonSerializable(typeof(FileRequestRequest)), JsonSerializable(typeof(RequestFiledResponse))]
[JsonSerializable(typeof(DecideRequest)), JsonSerializable(typeof(DecideBody)), JsonSerializable(typeof(ListRequestsRequest)), JsonSerializable(typeof(ListRequestsResponse))]
[JsonSerializable(typeof(RequestView)), JsonSerializable(typeof(ListDecisionsRequest)), JsonSerializable(typeof(ListDecisionsResponse)), JsonSerializable(typeof(DecisionView))]
[JsonSerializable(typeof(OptionView)), JsonSerializable(typeof(GetNeedsYouRequest)), JsonSerializable(typeof(GetNeedsYouResponse)), JsonSerializable(typeof(GetItemTimelineRequest))]
[JsonSerializable(typeof(TimelineResponse)), JsonSerializable(typeof(Recipe)), JsonSerializable(typeof(BlockDescriptor)), JsonSerializable(typeof(ValidateRecipeResponse))]
[JsonSerializable(typeof(EvalRecipeResponse)), JsonSerializable(typeof(WorkflowEvent)), JsonSerializable(typeof(EvalStartLine)), JsonSerializable(typeof(EvalStepLine))]
[JsonSerializable(typeof(RecipeValidationView)), JsonSerializable(typeof(CompleteStepResponse)), JsonSerializable(typeof(InstanceView)), JsonSerializable(typeof(ListOutboxResponse))]
[JsonSerializable(typeof(RequestKind))]   // API P8: the sindri_request kind argument binds through it
[JsonSerializable(typeof(DeskNotice))]   // engine P6 unit (c): a line of notifications.jsonl
[JsonSerializable(typeof(EffectRow))]            // engine P6: an effect's ledger row (§5.7)
[JsonSerializable(typeof(EffectReply))]          // engine P6 unit (a): an effect's reply event (notify.delivered, claim.created)
[JsonSerializable(typeof(GetInstanceEventsResponse)), JsonSerializable(typeof(InstanceEventView)), JsonSerializable(typeof(InstanceEventFields)), JsonSerializable(typeof(StartInstanceResponse))]
[JsonSerializable(typeof(StartInstanceRequest)), JsonSerializable(typeof(ApplyCommandRequest)), JsonSerializable(typeof(CompleteStepRequest)), JsonSerializable(typeof(ApplyCommandResponse))]
[JsonSerializable(typeof(IssueNonceResponse)), JsonSerializable(typeof(RecipeRegistered)), JsonSerializable(typeof(RecipeApproved)), JsonSerializable(typeof(RecipeRevoked))]
[JsonSerializable(typeof(RegisterRecipeRequest)), JsonSerializable(typeof(RegisterRecipeResponse)), JsonSerializable(typeof(ListRecipesResponse)), JsonSerializable(typeof(ApproveRecipeRequest))]
[JsonSerializable(typeof(ApproveRecipeResponse)), JsonSerializable(typeof(RevokeRecipeRequest)), JsonSerializable(typeof(RevokeRecipeResponse)), JsonSerializable(typeof(IssueChallengeRequest))]
[JsonSerializable(typeof(IssueChallengeResponse)), JsonSerializable(typeof(HarnessProbeReport)), JsonSerializable(typeof(ChallengeAnswer)), JsonSerializable(typeof(ContextResult))]
[JsonSerializable(typeof(NodeView)), JsonSerializable(typeof(NeighborsView)), JsonSerializable(typeof(PathView)), JsonSerializable(typeof(ProbeSet))]
[JsonSerializable(typeof(ProbeReport)), JsonSerializable(typeof(ProbeRun)), JsonSerializable(typeof(SourcesView)), JsonSerializable(typeof(SourceSyncStatus))]
[JsonSerializable(typeof(McpToolError)), JsonSerializable(typeof(McpSourcesAnswer)), JsonSerializable(typeof(RegisterMcpResponse)), JsonSerializable(typeof(ContextSourcesSynced))]
[JsonSerializable(typeof(ContextLinksSynced)), JsonSerializable(typeof(ContextMapImported)), JsonSerializable(typeof(MapDocument)), JsonSerializable(typeof(ReviewFile))]
[JsonSerializable(typeof(MapStatusView)), JsonSerializable(typeof(MapExported)), JsonSerializable(typeof(MapHealth)), JsonSerializable(typeof(PurgeReport))]
[JsonSerializable(typeof(ImportContextMapRequest)), JsonSerializable(typeof(ImportContextMapResponse))]
// Two RecordKey types meet here: Sindri's item link key and the context lane's source key; name Sindri's apart (SYSLIB1031).
[JsonSerializable(typeof(Core.Ids.RecordKey), TypeInfoPropertyName = "ItemRecordKey")]
[JsonSerializable(typeof(BudgetFigures)), JsonSerializable(typeof(ChildGroup)), JsonSerializable(typeof(ChildRow)), JsonSerializable(typeof(CurrentState))]
[JsonSerializable(typeof(DeskForm)), JsonSerializable(typeof(DeskOutput)), JsonSerializable(typeof(EngineOwnedBadge)), JsonSerializable(typeof(FactPending))]
[JsonSerializable(typeof(FactRef)), JsonSerializable(typeof(FactRow)), JsonSerializable(typeof(GateLine)), JsonSerializable(typeof(InputPresence))]
[JsonSerializable(typeof(InstanceDetail)), JsonSerializable(typeof(InstanceList)), JsonSerializable(typeof(InstanceRecipe)), JsonSerializable(typeof(InstanceSummary))]
[JsonSerializable(typeof(LoopPair)), JsonSerializable(typeof(Progress)), JsonSerializable(typeof(RecipeInputView)), JsonSerializable(typeof(RecipeLoopView))]
[JsonSerializable(typeof(RecipeOutputView)), JsonSerializable(typeof(RecipeStateView)), JsonSerializable(typeof(RecipeView)), JsonSerializable(typeof(Round))]
[JsonSerializable(typeof(StateCell)), JsonSerializable(typeof(StatusAnswer)), JsonSerializable(typeof(StatusBudget)), JsonSerializable(typeof(StatusChild))]
[JsonSerializable(typeof(StatusCommand)), JsonSerializable(typeof(StatusDrift)), JsonSerializable(typeof(StatusFields)), JsonSerializable(typeof(StatusHeader))]
[JsonSerializable(typeof(StatusInput)), JsonSerializable(typeof(StatusPaused)), JsonSerializable(typeof(StatusTimeout)), JsonSerializable(typeof(StepDeclaration))]
[JsonSerializable(typeof(SubjectRef)), JsonSerializable(typeof(ViewCounts)), JsonSerializable(typeof(Workstream)), JsonSerializable(typeof(StateChip))]
[JsonSerializable(typeof(ListCounts)), JsonSerializable(typeof(ParentRef)), JsonSerializable(typeof(FactView)), JsonSerializable(typeof(MapImportChallenge))]
[JsonSerializable(typeof(ContextChallenge)), JsonSerializable(typeof(ReviewContextMapRequest)), JsonSerializable(typeof(ReviewContextMapResponse)), JsonSerializable(typeof(PromoteContextMapRequest))]
[JsonSerializable(typeof(PromoteContextMapResponse)), JsonSerializable(typeof(ProposeContextMapRequest)), JsonSerializable(typeof(ProposeContextMapResponse)), JsonSerializable(typeof(ReviewQueueView))]
[JsonSerializable(typeof(ReviewHistoryView)), JsonSerializable(typeof(ReviewExported)), JsonSerializable(typeof(ReviewResult)), JsonSerializable(typeof(ProposalsView))]
[JsonSerializable(typeof(LensesView)), JsonSerializable(typeof(LensesSetView)), JsonSerializable(typeof(FileMapView)), JsonSerializable(typeof(ContextMapReviewed)), JsonSerializable(typeof(ContextMapPromoted))]
[JsonSerializable(typeof(ContextMapProposed)), JsonSerializable(typeof(ContextMapBuilt))]
[JsonSerializable(typeof(FleetChanged))]
[JsonSerializable(typeof(ItemOpenRequested)), JsonSerializable(typeof(LaunchStarted)), JsonSerializable(typeof(LaunchStartedPublic)), JsonSerializable(typeof(LaunchFailed))]   // API P10a (U0)
[JsonSerializable(typeof(LaunchShowReport)), JsonSerializable(typeof(LaunchProbed)), JsonSerializable(typeof(SessionEnded)), JsonSerializable(typeof(SessionUsage)), JsonSerializable(typeof(LaunchSessionReported))]
[JsonSerializable(typeof(LaunchViewerAttached))]
[JsonSerializable(typeof(GetLaunchTargetsRequest)), JsonSerializable(typeof(LaunchTargets)), JsonSerializable(typeof(ProbeLaunchTargetsRequest)), JsonSerializable(typeof(ProbeLaunchTargetsResult))]   // API P10a (U11): the launch targets and their probe job
[JsonSerializable(typeof(LaunchRow)), JsonSerializable(typeof(LaunchEnd)), JsonSerializable(typeof(LaunchViewer)), JsonSerializable(typeof(LaunchViewerPublic))]   // API P10a (U8): the launches view
[JsonSerializable(typeof(OpenItemRequest)), JsonSerializable(typeof(OpenItemBody)), JsonSerializable(typeof(LaunchRequested)), JsonSerializable(typeof(GetLaunchRequest)), JsonSerializable(typeof(LaunchView))]
[JsonSerializable(typeof(SessionHeartbeatBody)), JsonSerializable(typeof(SessionHeartbeatResponse)), JsonSerializable(typeof(SessionExitBody)), JsonSerializable(typeof(SessionExitResponse)), JsonSerializable(typeof(CaptureRule))]   // API P10a (U10)
// Two LaunchId types meet here: the collector's looser one (Core.Launch) and the engine's 'LCH-…' one the launch events carry (SYSLIB1031).
[JsonSerializable(typeof(Core.Workflow.Engine.LaunchId), TypeInfoPropertyName = "EngineLaunchId")]
[JsonSerializable(typeof(SyncContextSourcesRequest)), JsonSerializable(typeof(SyncContextSourcesResult))]
[JsonSerializable(typeof(BuildContextMapRequest)), JsonSerializable(typeof(BuildContextMapResult))]
[JsonSerializable(typeof(Context.Core.Eval.MapQuality.MapQualityRun)), JsonSerializable(typeof(Context.Core.Eval.Backtest.BacktestRun)), JsonSerializable(typeof(Context.Core.Eval.Backtest.EvalPurgeReport))]
[JsonSerializable(typeof(RunContextBacktestRequest)), JsonSerializable(typeof(RunContextBacktestResult)), JsonSerializable(typeof(BacktestArmLine))]
internal sealed partial class SindriJsonContext : JsonSerializerContext;
