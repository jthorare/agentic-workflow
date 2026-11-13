using System.Text.Json.Serialization;
using Aw.Context.Core.Code;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Graph;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Pages;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Context.Core.ScopeGraph;
using Aw.Context.Core.Search;
using Aw.Context.Core.Subgraph;
using Aw.Context.Core.WholeMap;
using Aw.Kernel;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Handoff;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Core.ModelHistory;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.AddRepo;
using Aw.Sindri.Features.AgentNodeControl;
using Aw.Sindri.Features.AllowDenial;
using Aw.Sindri.Features.AnswerQuestion;
using Aw.Sindri.Features.ApplyWorkflowOps;
using Aw.Sindri.Features.AttachProject;
using Aw.Sindri.Features.CancelJob;
using Aw.Sindri.Features.CancelWorkflowRun;
using Aw.Sindri.Features.CheckSandbox;
using Aw.Sindri.Features.CloseItem;
using Aw.Sindri.Features.CodeIndexJobs;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Features.Decide;
using Aw.Sindri.Features.DeleteWorkflow;
using Aw.Sindri.Features.EndLearnMode;
using Aw.Sindri.Features.FileRequest;
using Aw.Sindri.Features.GetConnection;
using Aw.Sindri.Features.GetEventCatalog;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.GetItemTimeline;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Features.GetLastStart;
using Aw.Sindri.Features.GetLaunch;
using Aw.Sindri.Features.GetLaunchPicks;
using Aw.Sindri.Features.GetLaunchTargets;
using Aw.Sindri.Features.GetLibrary;
using Aw.Sindri.Features.GetLibraryDoc;
using Aw.Sindri.Features.GetLinearLookups;
using Aw.Sindri.Features.GetLinearSince;
using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Features.GetProfile;
using Aw.Sindri.Features.GetProject;
using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Features.GetRepoStatus;
using Aw.Sindri.Features.GetReviewPage;
using Aw.Sindri.Features.GetRunSuspension;
using Aw.Sindri.Features.GetSession;
using Aw.Sindri.Features.GetSetup;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.GetWorkflow;
using Aw.Sindri.Features.GetWorkflowRun;
using Aw.Sindri.Features.HideProject;
using Aw.Sindri.Features.InitProfile;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.LinearConnection;
using Aw.Sindri.Features.LinearIssues;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Features.ListConnections;
using Aw.Sindri.Features.ListDecisions;
using Aw.Sindri.Features.ListDenials;
using Aw.Sindri.Features.ListItems;
using Aw.Sindri.Features.ListJobs;
using Aw.Sindri.Features.ListLinearProjects;
using Aw.Sindri.Features.ListProjects;
using Aw.Sindri.Features.ListRequests;
using Aw.Sindri.Features.ListReviews;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Features.ListSessions;
using Aw.Sindri.Features.ListWorkflowBlocks;
using Aw.Sindri.Features.ListWorkflowRuns;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Features.OnboardRepo;
using Aw.Sindri.Features.OpenItem;
using Aw.Sindri.Features.PauseService;
using Aw.Sindri.Features.PauseWorkflow;
using Aw.Sindri.Features.PollConnections;
using Aw.Sindri.Features.ProbeLaunchTargets;
using Aw.Sindri.Features.ReadLedger;
using Aw.Sindri.Features.RebuildViews;
using Aw.Sindri.Features.RecordLimitHit;
using Aw.Sindri.Features.RegisterMcp;
using Aw.Sindri.Features.RemoveConnection;
using Aw.Sindri.Features.RemoveSessionWorktree;
using Aw.Sindri.Features.RenameConnection;
using Aw.Sindri.Features.ReplaceWorkflow;
using Aw.Sindri.Features.ResumeJob;
using Aw.Sindri.Features.ResumeService;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.RunService;
using Aw.Sindri.Features.RunWorkflow;
using Aw.Sindri.Features.Scrub;
using Aw.Sindri.Features.SearchWorkflowDocs;
using Aw.Sindri.Features.SendReviewFix;
using Aw.Sindri.Features.SessionExit;
using Aw.Sindri.Features.SessionHeartbeat;
using Aw.Sindri.Features.SetProfileKey;
using Aw.Sindri.Features.SetReviewLogin;
using Aw.Sindri.Features.StartSession;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Features.TestConnection;
using Aw.Sindri.Features.ValidateProfile;
using Aw.Sindri.Features.ValidateWorkflow;
using Aw.Sindri.Host.Commands.Probe;
using Aw.Sindri.Host.Endpoints;
using Aw.Sindri.Host.Mcp;

namespace Aw.Sindri.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(AgentLimits))]
[JsonSerializable(typeof(Error)), JsonSerializable(typeof(SindriProfile)), JsonSerializable(typeof(ControlEndpoint)), JsonSerializable(typeof(Ping))]
[JsonSerializable(typeof(ApiProblem)), JsonSerializable(typeof(ApiProblemBuilding)), JsonSerializable(typeof(ApiProblemNoMap)), JsonSerializable(typeof(ProcessStarted)), JsonSerializable(typeof(ProcessStopped)), JsonSerializable(typeof(WorkflowEventPayload))]
[JsonSerializable(typeof(ProfileApproved)), JsonSerializable(typeof(ProfileApprovalsRemoved)), JsonSerializable(typeof(Heartbeat)), JsonSerializable(typeof(CommandHandled)), JsonSerializable(typeof(ServiceRan))]
[JsonSerializable(typeof(ProfileChecked)), JsonSerializable(typeof(ViewsRebuilt)), JsonSerializable(typeof(RetirePassDone)), JsonSerializable(typeof(ItemCreated)), JsonSerializable(typeof(ItemCreatedPublic))]
[JsonSerializable(typeof(ProjectAttached)), JsonSerializable(typeof(ProjectChanged)), JsonSerializable(typeof(ProjectDetached)), JsonSerializable(typeof(ProjectHidden)), JsonSerializable(typeof(ProjectLedNoted)), JsonSerializable(typeof(ProjectAttachedRow)), JsonSerializable(typeof(ProjectHiddenRow))]   // projects page Ph3: the owner-state events and their views
[JsonSerializable(typeof(LinearWriteRecorded))]   // the Linear write events (agent-first PR 2)
[JsonSerializable(typeof(TodoAdded)), JsonSerializable(typeof(TodoChanged)), JsonSerializable(typeof(TodoDone)), JsonSerializable(typeof(TodoReopened)), JsonSerializable(typeof(TodoRemoved)), JsonSerializable(typeof(TodoRestored))]   // the ledger events of the todos spec (§4.1)
[JsonSerializable(typeof(TodoConflicted)), JsonSerializable(typeof(TodoConflictResolved)), JsonSerializable(typeof(TodoConflictReopened)), JsonSerializable(typeof(TodoSide)), JsonSerializable(typeof(Core.Todos.TodoRow)), JsonSerializable(typeof(Core.Todos.TodoOpenConflict)), JsonSerializable(typeof(TodoHeldChange)), JsonSerializable(typeof(TodoNoteHeld)), JsonSerializable(typeof(TodoNoteHeldResolved)), JsonSerializable(typeof(TodoNoteHeldReopened)), JsonSerializable(typeof(TodoNoteSynced)), JsonSerializable(typeof(TodoNoteImported))]
[JsonSerializable(typeof(ItemHandoff)), JsonSerializable(typeof(Features.CreateHandoffItem.CreateHandoffItemRequest)), JsonSerializable(typeof(Features.CreateHandoffItem.HandoffItemCreatedResponse))]
[JsonSerializable(typeof(ItemStateChanged)), JsonSerializable(typeof(ItemLinked)), JsonSerializable(typeof(RequestFiled)), JsonSerializable(typeof(RequestFiledPublic))]
[JsonSerializable(typeof(DecisionRecorded)), JsonSerializable(typeof(DecisionRecordedPublic)), JsonSerializable(typeof(JobStarted))]
[JsonSerializable(typeof(StartSessionRequest)), JsonSerializable(typeof(StartedSession)), JsonSerializable(typeof(StartedHost)), JsonSerializable(typeof(RemoveSessionWorktreeRequest)), JsonSerializable(typeof(RemovedWorktree)), JsonSerializable(typeof(AgentOpRecorded)), JsonSerializable(typeof(GetLastStartRequest)), JsonSerializable(typeof(LastStartView))]   // agents spec §15.8 (A7)
[JsonSerializable(typeof(JobProgress)), JsonSerializable(typeof(JobFinished)), JsonSerializable(typeof(JobFinishedPublic)), JsonSerializable(typeof(JobCancelled)), JsonSerializable(typeof(JobWaiting)), JsonSerializable(typeof(JobResumed))]
[JsonSerializable(typeof(ServicePaused)), JsonSerializable(typeof(ServiceResumed)), JsonSerializable(typeof(JobRow)), JsonSerializable(typeof(JobLine))]
[JsonSerializable(typeof(ContextModelCalled)), JsonSerializable(typeof(ModelCallTry)), JsonSerializable(typeof(ModelCallAnswerer)), JsonSerializable(typeof(ModelCallHow)), JsonSerializable(typeof(ModelTotals)), JsonSerializable(typeof(ModelAnsweredCount))]
[JsonSerializable(typeof(SetupStepChanged)), JsonSerializable(typeof(RepoAdded)), JsonSerializable(typeof(RepoAddedPublic)), JsonSerializable(typeof(RepoOnboarded)), JsonSerializable(typeof(ContextPinWritten)), JsonSerializable(typeof(ContextPageWritten)), JsonSerializable(typeof(RecipeRegistrationCompleted)), JsonSerializable(typeof(ConnectionNeedsSignIn)), JsonSerializable(typeof(ConnectionSignedIn)), JsonSerializable(typeof(ConnectionPolled)), JsonSerializable(typeof(ConnectionRemoved)), JsonSerializable(typeof(ConnectionSignInRequested)), JsonSerializable(typeof(MemoryFactsForgotten)), JsonSerializable(typeof(WorkflowTriggerSkipped)), JsonSerializable(typeof(CatchUpCapped))]   // connections PR 10b: the sign-in events; PR 11: the poll events
[JsonSerializable(typeof(McpRegistered)), JsonSerializable(typeof(McpProviderOutcome)), JsonSerializable(typeof(SetupConsentGiven))]
[JsonSerializable(typeof(JobLinks)), JsonSerializable(typeof(JobStartedResponse)), JsonSerializable(typeof(RunServiceRequest)), JsonSerializable(typeof(PauseServiceRequest))]
[JsonSerializable(typeof(ResumeServiceRequest)), JsonSerializable(typeof(CancelJobRequest)), JsonSerializable(typeof(ResumeJobRequest)), JsonSerializable(typeof(GetJobRequest)), JsonSerializable(typeof(JobView))]
[JsonSerializable(typeof(ListJobsRequest)), JsonSerializable(typeof(JobList)), JsonSerializable(typeof(JobSummary)), JsonSerializable(typeof(ScheduledResult))]
[JsonSerializable(typeof(AsOf)), JsonSerializable(typeof(ViewsStatus)), JsonSerializable(typeof(ServiceRow)), JsonSerializable(typeof(ProfileRow))]
[JsonSerializable(typeof(ItemRow)), JsonSerializable(typeof(ItemLinkRow)), JsonSerializable(typeof(AliasRow)), JsonSerializable(typeof(RequestRow)), JsonSerializable(typeof(HumanDecisionRow))]
[JsonSerializable(typeof(NeedsYouRowRecord)), JsonSerializable(typeof(NestedPark)), JsonSerializable(typeof(NeedsYouRow)), JsonSerializable(typeof(NeedsYouDecision)), JsonSerializable(typeof(NeedsYouDecisionOption)), JsonSerializable(typeof(NeedsYouStop)), JsonSerializable(typeof(NeedsYouSubject)), JsonSerializable(typeof(TimelineRow))]
[JsonSerializable(typeof(HealthReport)), JsonSerializable(typeof(CommandAccepted)), JsonSerializable(typeof(GetStatusResponse)), JsonSerializable(typeof(StopProcessResponse))]
[JsonSerializable(typeof(ValidateProfileResponse)), JsonSerializable(typeof(InitProfileResponse)), JsonSerializable(typeof(SetProfileKeyResponse)), JsonSerializable(typeof(ScrubResponse)), JsonSerializable(typeof(RunDoctorResponse))]
[JsonSerializable(typeof(PublicEvent)), JsonSerializable(typeof(LedgerPage)), JsonSerializable(typeof(EventCatalogView)), JsonSerializable(typeof(EventTypeView))]
[JsonSerializable(typeof(StreamErrorFrame)), JsonSerializable(typeof(InstanceChanged)), JsonSerializable(typeof(ListCommandsRequest)), JsonSerializable(typeof(ListCommandsResponse)), JsonSerializable(typeof(CommandListRow))]
[JsonSerializable(typeof(ListServicesRequest)), JsonSerializable(typeof(ListServicesResponse)), JsonSerializable(typeof(ServiceView)), JsonSerializable(typeof(GetProfileRequest))]
[JsonSerializable(typeof(SetReviewLoginRequest)), JsonSerializable(typeof(SetReviewLoginResponse))]
[JsonSerializable(typeof(GetLinearConnectionRequest)), JsonSerializable(typeof(LinearConnectionView)), JsonSerializable(typeof(LinearKeyStateView)), JsonSerializable(typeof(LinearConnectionTeamView)), JsonSerializable(typeof(ListLinearTeamsRequest)), JsonSerializable(typeof(ListLinearTeamsResponse)), JsonSerializable(typeof(SaveLinearConnectionRequest)), JsonSerializable(typeof(SaveLinearConnectionResponse)), JsonSerializable(typeof(LinearSavedWriteView)), JsonSerializable(typeof(RemoveLinearWriteKeyRequest)), JsonSerializable(typeof(RemoveLinearWriteKeyResponse))]   // Linear hub 1b: Settings, Connections, Linear
[JsonSerializable(typeof(SendReviewFixRequest)), JsonSerializable(typeof(SendReviewFixResponse)), JsonSerializable(typeof(ReviewFixBody)), JsonSerializable(typeof(ReviewFixSent))]
[JsonSerializable(typeof(GetSetupRequest)), JsonSerializable(typeof(GetSetupResponse)), JsonSerializable(typeof(GetRepoStatusResponse)), JsonSerializable(typeof(RepoStatus))]
[JsonSerializable(typeof(GetProfileResponse)), JsonSerializable(typeof(ProfileSummary)), JsonSerializable(typeof(RepoPreview)), JsonSerializable(typeof(RebuildViewsRequest)), JsonSerializable(typeof(CreateItemRequest)), JsonSerializable(typeof(ItemCreatedResponse))]
[JsonSerializable(typeof(Features.AddTodo.AddTodoRequest)), JsonSerializable(typeof(AddTodoBody)), JsonSerializable(typeof(Features.ChangeTodo.ChangeTodoRequest)), JsonSerializable(typeof(ChangeTodoBody)), JsonSerializable(typeof(Features.MarkTodo.MarkTodoRequest)), JsonSerializable(typeof(MarkTodoBody)), JsonSerializable(typeof(Core.Todos.TodoAccepted)), JsonSerializable(typeof(Features.ListTodos.ListTodosRequest)), JsonSerializable(typeof(Features.ListTodos.ListTodosResponse)), JsonSerializable(typeof(Features.GetTodo.GetTodoResponse)), JsonSerializable(typeof(Features.GetTodoNote.GetTodoNoteResponse)), JsonSerializable(typeof(Core.Todos.TodoCounts)), JsonSerializable(typeof(Core.Todos.TodoListState)), JsonSerializable(typeof(Core.Todos.TodoItem)), JsonSerializable(typeof(Core.Todos.TodoLinkItem)), JsonSerializable(typeof(Features.CheckTodoNote.CheckTodoNoteRequest)), JsonSerializable(typeof(Features.CheckTodoNote.CheckTodoNoteResponse)), JsonSerializable(typeof(Features.SetTodoNote.SetTodoNoteRequest)), JsonSerializable(typeof(Features.StopTodoNote.StopTodoNoteRequest)), JsonSerializable(typeof(Core.Todos.TodoNoteWritten)), JsonSerializable(typeof(Core.Todos.TodoNoteEdit)), JsonSerializable(typeof(Core.Todos.Note.NoteCantImport)), JsonSerializable(typeof(CheckTodoNoteBody)), JsonSerializable(typeof(Features.ImportTodoNote.ImportTodoNoteRequest)), JsonSerializable(typeof(Features.ImportTodoNote.ImportTodoNoteResponse)), JsonSerializable(typeof(ImportTodoNoteBody)), JsonSerializable(typeof(SetTodoNoteBody)), JsonSerializable(typeof(StopTodoNoteBody)), JsonSerializable(typeof(Features.KeepTodo.KeepTodoRequest)), JsonSerializable(typeof(KeepTodoWire)), JsonSerializable(typeof(KeepTodoUndoWire)), JsonSerializable(typeof(KeepTodoBody)), JsonSerializable(typeof(Features.KeepTodoUndo.KeepTodoUndoRequest)), JsonSerializable(typeof(Features.PickTodoHold.PickTodoHoldRequest)), JsonSerializable(typeof(Features.PickTodoHold.PickTodoHoldResponse)), JsonSerializable(typeof(HoldPickBody)), JsonSerializable(typeof(Features.UndoTodoHold.UndoTodoHoldRequest)), JsonSerializable(typeof(Features.UndoTodoHold.UndoTodoHoldResponse))]   // the todo writes and reads (todo spec §4.4)
[JsonSerializable(typeof(AttachProjectRequest)), JsonSerializable(typeof(AttachProjectBody)), JsonSerializable(typeof(ProjectAccepted)), JsonSerializable(typeof(ChangeProjectBody)), JsonSerializable(typeof(DetachProjectBody)), JsonSerializable(typeof(HideProjectRequest))]
[JsonSerializable(typeof(ListReviewsRequest)), JsonSerializable(typeof(ListReviewsResponse))]   // PR review page Ph5: the Reviews list
[JsonSerializable(typeof(GetReviewPageRequest)), JsonSerializable(typeof(ReviewPageModel))]   // PR review page Ph5c: the PR page
[JsonSerializable(typeof(RequiredChecksData)), JsonSerializable(typeof(DocMapData)), JsonSerializable(typeof(TestPlanData)), JsonSerializable(typeof(CiData)), JsonSerializable(typeof(EvidenceData)), JsonSerializable(typeof(RiskData)), JsonSerializable(typeof(ConversationData))]   // the panels' rows, picked by ReviewPagePanelConverter
[JsonSerializable(typeof(GetLibraryRequest)), JsonSerializable(typeof(GetLibraryResponse))]   // Ph5b: the library page
[JsonSerializable(typeof(GetLibraryDocRequest)), JsonSerializable(typeof(GetLibraryDocResponse)), JsonSerializable(typeof(LibraryDocView)), JsonSerializable(typeof(LibraryDocErrorView))]   // PR review page Ph5b: one design doc
[JsonSerializable(typeof(CommentLinearIssueRequest)), JsonSerializable(typeof(CommentLinearIssueBody)), JsonSerializable(typeof(CommentLinearIssueResponse)), JsonSerializable(typeof(CreateLinearIssueRequest)), JsonSerializable(typeof(CreateLinearIssueResponse))]   // Linear hub Ph3 1b: the comment and create writes
[JsonSerializable(typeof(ListLinearIssuesRequest)), JsonSerializable(typeof(ListLinearIssuesResponse)), JsonSerializable(typeof(GetLinearIssueRequest)), JsonSerializable(typeof(GetLinearIssueResponse))]   // Linear hub Ph3 1a: the Work page's Linear list
[JsonSerializable(typeof(Commands.Library.LibraryValidateReport)), JsonSerializable(typeof(Commands.Library.LibraryFindingRow))]   // PR review page Ph5b: 'sindri library list' and 'validate'
[JsonSerializable(typeof(ChangeLinearIssueRequest)), JsonSerializable(typeof(ChangeLinearIssueBody)), JsonSerializable(typeof(LinearChangeApplied)), JsonSerializable(typeof(LinearChangeResult)), JsonSerializable(typeof(LinearFieldConflict[]))]   // Linear hub Ph3 1b: the issue edit
[JsonSerializable(typeof(ListProjectsRequest)), JsonSerializable(typeof(ListProjectsResponse)), JsonSerializable(typeof(GetProjectRequest)), JsonSerializable(typeof(GetProjectResponse)), JsonSerializable(typeof(ProjectCard)), JsonSerializable(typeof(ListLinearProjectsRequest)), JsonSerializable(typeof(ListLinearProjectsResponse)), JsonSerializable(typeof(LinearProjectChoice))]
[JsonSerializable(typeof(AddConnectionRequest)), JsonSerializable(typeof(AddConnectionResponse)), JsonSerializable(typeof(TestConnectionRequest)), JsonSerializable(typeof(TestConnectionResponse)), JsonSerializable(typeof(ListConnectionsRequest)), JsonSerializable(typeof(ListConnectionsResponse)), JsonSerializable(typeof(GetConnectionRequest)), JsonSerializable(typeof(GetConnectionResponse)), JsonSerializable(typeof(RemoveConnectionRequest)), JsonSerializable(typeof(SignInConnectionRequest)), JsonSerializable(typeof(RemoveConnectionResponse)), JsonSerializable(typeof(RenameConnectionRequest)), JsonSerializable(typeof(RenameConnectionResponse)), JsonSerializable(typeof(RenameConnectionBody)), JsonSerializable(typeof(ConnectionView)), JsonSerializable(typeof(Features.CreateMemory.CreateMemoryRequest)), JsonSerializable(typeof(Features.CreateMemory.CreateMemoryResponse)), JsonSerializable(typeof(Features.ListMemories.ListMemoriesRequest)), JsonSerializable(typeof(Features.ListMemories.ListMemoriesResponse)), JsonSerializable(typeof(Features.SearchMemory.SearchMemoryRequest)), JsonSerializable(typeof(Features.SearchMemory.SearchMemoryResponse)), JsonSerializable(typeof(Features.WriteMemoryFact.WriteMemoryFactRequest)), JsonSerializable(typeof(Features.WriteMemoryFact.WriteMemoryFactResponse)), JsonSerializable(typeof(WriteMemoryFactBody)), JsonSerializable(typeof(Features.ForgetMemoryFact.ForgetMemoryFactRequest)), JsonSerializable(typeof(Features.ForgetMemoryFact.ForgetMemoryFactResponse)), JsonSerializable(typeof(Core.Memory.MemoryView)), JsonSerializable(typeof(Core.Memory.MemoryFactView)), JsonSerializable(typeof(Core.Memory.MemoryProvenance))]   // connections PR 10a, agent-first PR 13 (memory)
[JsonSerializable(typeof(ProjectLead)), JsonSerializable(typeof(ProjectLink)), JsonSerializable(typeof(ProjectFreshnessRow))]
[JsonSerializable(typeof(CloseItemRequest)), JsonSerializable(typeof(CloseItemBody)), JsonSerializable(typeof(GetQueueRequest)), JsonSerializable(typeof(QueueResponse)), JsonSerializable(typeof(QueueGroupCounts))]
[JsonSerializable(typeof(QueueGroupView)), JsonSerializable(typeof(ItemSummary)), JsonSerializable(typeof(ListItemsRequest)), JsonSerializable(typeof(ListItemsResponse))]
[JsonSerializable(typeof(GetItemRequest)), JsonSerializable(typeof(ItemView)), JsonSerializable(typeof(FileRequestRequest)), JsonSerializable(typeof(RequestFiledResponse))]
[JsonSerializable(typeof(DecideRequest)), JsonSerializable(typeof(DecideBody)), JsonSerializable(typeof(ListRequestsRequest)), JsonSerializable(typeof(ListRequestsResponse))]
[JsonSerializable(typeof(RequestView)), JsonSerializable(typeof(ListDecisionsRequest)), JsonSerializable(typeof(ListDecisionsResponse)), JsonSerializable(typeof(DecisionView))]
[JsonSerializable(typeof(OptionView)), JsonSerializable(typeof(GetNeedsYouRequest)), JsonSerializable(typeof(GetNeedsYouResponse)), JsonSerializable(typeof(GetItemTimelineRequest))]
[JsonSerializable(typeof(TimelineResponse)), JsonSerializable(typeof(BlockDescriptor))]

[JsonSerializable(typeof(WrapLimits))]   // SB-3a: the wrap limits file
[JsonSerializable(typeof(WorkflowEvent))]
[JsonSerializable(typeof(CompleteStepResponse)), JsonSerializable(typeof(InstanceView))]
[JsonSerializable(typeof(ScopingApproved)), JsonSerializable(typeof(ScopingEscape))]   // SC-K: the scoping verbs
// LN-K: the line verbs; LN-API1
// API17: 'docs check'
// LN-L: 'line doctor'
[JsonSerializable(typeof(RequestKind))]   // API P8: the sindri_request kind argument binds through it
[JsonSerializable(typeof(FleetChanged))]
[JsonSerializable(typeof(Features.CodeIndexReads.CodeIndexStatusView)), JsonSerializable(typeof(Features.CodeIndexReads.RepoHealth)), JsonSerializable(typeof(CodeIndex.Core.Facade.FindSymbolsAnswer)), JsonSerializable(typeof(CodeIndex.Core.Values.RepoName), TypeInfoPropertyName = "CodeIndexRepoName"), JsonSerializable(typeof(Features.CodeIndexReads.CodeCallsView)), JsonSerializable(typeof(CodeIndex.Core.Facade.RepoStatus), TypeInfoPropertyName = "CodeIndexRepoStatus")]   // code-index reads (CI-10a)
[JsonSerializable(typeof(SessionList)), JsonSerializable(typeof(SessionView)), JsonSerializable(typeof(SessionRow)), JsonSerializable(typeof(SessionSandbox)), JsonSerializable(typeof(SessionMemory)), JsonSerializable(typeof(EndedSessionRow)), JsonSerializable(typeof(SessionStatus))]   // collector spec CA-1, CA-2, CA-8
[JsonSerializable(typeof(LaunchPicks)), JsonSerializable(typeof(LaunchPickView)), JsonSerializable(typeof(StateEscalation)), JsonSerializable(typeof(PickRunRow)), JsonSerializable(typeof(PickRunDetail)), JsonSerializable(typeof(PickRunDelta)), JsonSerializable(typeof(PickRunFrame))]   // model-pick spec §6.1: GET /sindri/launch/picks
[JsonSerializable(typeof(DeskNotice))]   // engine P6 unit (c): a line of notifications.jsonl
[JsonSerializable(typeof(EffectRow))]            // engine P6: an effect's ledger row (§5.7)
[JsonSerializable(typeof(RoutedItem))]           // engine P7 plan D8: the router stub's item.routed event
[JsonSerializable(typeof(EffectReply))]          // engine P6 unit (a): an effect's reply event (notify.delivered, claim.created)

[JsonSerializable(typeof(CompleteStepRequest))]
[JsonSerializable(typeof(ListWorkflowBlocksResponse)), JsonSerializable(typeof(SearchWorkflowDocsResponse))]   // #2258 PR 8b: workflow_blocks and workflow_docs
[JsonSerializable(typeof(CreateWorkflowBody)), JsonSerializable(typeof(ApplyWorkflowOpsBody)), JsonSerializable(typeof(ReplaceWorkflowBody)), JsonSerializable(typeof(ValidateWorkflowBody)), JsonSerializable(typeof(ActivateWorkflowBody))]   // #2258 PR 8b: the workflow write bodies
[JsonSerializable(typeof(CreateWorkflowResponse)), JsonSerializable(typeof(ApplyWorkflowOpsResponse)), JsonSerializable(typeof(ReplaceWorkflowResponse)), JsonSerializable(typeof(ValidateWorkflowResponse)), JsonSerializable(typeof(ActivateWorkflowResponse)), JsonSerializable(typeof(PauseWorkflowResponse)), JsonSerializable(typeof(DeleteWorkflowResponse))]
[JsonSerializable(typeof(ListWorkflowsResponse)), JsonSerializable(typeof(GetWorkflowResponse)), JsonSerializable(typeof(ListWorkflowRunsResponse)), JsonSerializable(typeof(GetWorkflowRunResponse))]   // #2258 PR 8a: the workflow reads; GraphSpec and the node positions come with GetWorkflowResponse
[JsonSerializable(typeof(WorkflowNotifyPayload)), JsonSerializable(typeof(RunWorkflowBody)), JsonSerializable(typeof(RunWorkflowResponse)), JsonSerializable(typeof(CancelWorkflowRunResponse)), JsonSerializable(typeof(AgentNodeBody)), JsonSerializable(typeof(AgentNodeControlled))]
[JsonSerializable(typeof(TestWorkflowBody)), JsonSerializable(typeof(PinWorkflowOutputBody)), JsonSerializable(typeof(Features.TestWorkflow.TestWorkflowResponse)), JsonSerializable(typeof(Features.PinWorkflowOutput.PinWorkflowOutputResponse)), JsonSerializable(typeof(ListTriggerEventsResponse)), JsonSerializable(typeof(Features.ListWorkflowTemplates.ListWorkflowTemplatesResponse))]   // Workflows page P0 (#3191): the test, pin, trigger-events and templates contracts
[JsonSerializable(typeof(WorkflowChangedPublic)), JsonSerializable(typeof(WorkflowRunPublic)), JsonSerializable(typeof(WorkflowNodePublic))]   // issue 2823: the public forms of the workflow lifecycle, run and node events
[JsonSerializable(typeof(WorkflowQuestionPayload)), JsonSerializable(typeof(WorkflowAnsweredPayload)), JsonSerializable(typeof(WorkflowQuestionPublic)), JsonSerializable(typeof(WorkflowAnsweredPublic)), JsonSerializable(typeof(AnswerQuestionBody)), JsonSerializable(typeof(AnswerQuestionResponse))]   // agent-first PR 9: ask_owner and 'sindri answer'
[JsonSerializable(typeof(Features.ListResources.ListResourcesRequest)), JsonSerializable(typeof(Features.ListResources.ListResourcesResponse)), JsonSerializable(typeof(Features.ListResources.ResourceView)), JsonSerializable(typeof(Features.PurgeResources.PurgeResourcesRequest)), JsonSerializable(typeof(Features.PurgeResources.PurgeResourcesResponse))]   // agent-first PR 14: resources
[JsonSerializable(typeof(Features.StartToolkit.StartToolkitRequest)), JsonSerializable(typeof(Features.StartToolkit.StartToolkitResponse)), JsonSerializable(typeof(Features.StopToolkit.StopToolkitRequest)), JsonSerializable(typeof(Features.StopToolkit.StopToolkitResponse)), JsonSerializable(typeof(Features.PauseToolkit.PauseToolkitRequest)), JsonSerializable(typeof(Features.PauseToolkit.PauseToolkitResponse)), JsonSerializable(typeof(Features.ResumeToolkit.ResumeToolkitRequest)), JsonSerializable(typeof(Features.ResumeToolkit.ResumeToolkitResponse)), JsonSerializable(typeof(Features.CreateToolkit.CreateToolkitRequest)), JsonSerializable(typeof(Features.CreateToolkit.CreateToolkitResult)), JsonSerializable(typeof(Features.BuildToolkit.BuildToolkitRequest)), JsonSerializable(typeof(Features.BuildToolkit.BuildToolkitResult)), JsonSerializable(typeof(Features.ListToolkits.ListToolkitsResponse)), JsonSerializable(typeof(Features.ListToolkitCalls.ListToolkitCallsResponse)), JsonSerializable(typeof(Core.Toolkits.Toolkit)), JsonSerializable(typeof(Core.Toolkits.ToolkitCall))]   // agent-first PR 16c: toolkits
[JsonSerializable(typeof(RecipeRegistered)), JsonSerializable(typeof(RecipeApproved)), JsonSerializable(typeof(RecipeRevoked))]

[JsonSerializable(typeof(RegisterMcpJobRequest))]   // setup-once §5.4: register Sindri in a repo from the app
[JsonSerializable(typeof(OnboardRepoRequest)), JsonSerializable(typeof(OnboardedRepo))]   // setup-once §5.1: onboard a repo of the approved profile by hand
[JsonSerializable(typeof(AddRepoRequest)), JsonSerializable(typeof(AddedRepo))]   // setup-once §5.1: add a repo from the app
// setup-once §7.2: the plan, the hold's job and its result
[JsonSerializable(typeof(IssueChallengeRequest)), JsonSerializable(typeof(IssueChallengeResponse)), JsonSerializable(typeof(IssueAppChallengeRequest)), JsonSerializable(typeof(HarnessProbeReport)), JsonSerializable(typeof(ChallengeAnswer)), JsonSerializable(typeof(ContextResult))]
[JsonSerializable(typeof(NodeView)), JsonSerializable(typeof(NeighborsView)), JsonSerializable(typeof(PathView)), JsonSerializable(typeof(ProbeSet))]
[JsonSerializable(typeof(ProbeReport)), JsonSerializable(typeof(ProbeRun)), JsonSerializable(typeof(SourcesView)), JsonSerializable(typeof(SourceSyncStatus))]
[JsonSerializable(typeof(McpToolError)), JsonSerializable(typeof(McpSourcesAnswer)), JsonSerializable(typeof(RegisterMcpResponse)), JsonSerializable(typeof(Core.Mcp.ProviderResult)), JsonSerializable(typeof(ContextSourcesSynced))]
[JsonSerializable(typeof(ContextLinksSynced)), JsonSerializable(typeof(ContextMapImported)), JsonSerializable(typeof(MapDocument)), JsonSerializable(typeof(ReviewFile))]
[JsonSerializable(typeof(MapStatusView)), JsonSerializable(typeof(MapPublishStatusView)), JsonSerializable(typeof(StaleRepo)), JsonSerializable(typeof(MapExported)), JsonSerializable(typeof(MapHealth)), JsonSerializable(typeof(PurgeReport))]
[JsonSerializable(typeof(ImportContextMapRequest)), JsonSerializable(typeof(ImportContextMapResponse))]
[JsonSerializable(typeof(GetIndexStatusRequest)), JsonSerializable(typeof(IndexStatusView)), JsonSerializable(typeof(IndexSourceRow)), JsonSerializable(typeof(IndexConceptsView)), JsonSerializable(typeof(IndexStoreView)), JsonSerializable(typeof(PurgeIndexSourceRequest)), JsonSerializable(typeof(IndexPurgeView))]   // the index verbs (owned-index spec §5.8)
[JsonSerializable(typeof(CheckContextMapStructureRequest)), JsonSerializable(typeof(MapChecksView))]   // map structure spec §4.3
[JsonSerializable(typeof(PreviewPagesRequest)), JsonSerializable(typeof(PagesPreview)), JsonSerializable(typeof(PagesPreviewBlock)), JsonSerializable(typeof(PageListMatch)), JsonSerializable(typeof(PageListActionRow)), JsonSerializable(typeof(PageListActionMatch)), JsonSerializable(typeof(PageListConcept))]   // pages spec §3.6: Add several pages
// Two RecordKey types meet here: Sindri's item link key and the context lane's source key; name Sindri's apart (SYSLIB1031).
[JsonSerializable(typeof(Core.Ids.RecordKey), TypeInfoPropertyName = "ItemRecordKey")]




// API P12: GET /workflow/recipes/{id}/stats
[JsonSerializable(typeof(EngineView)), JsonSerializable(typeof(EngineInstanceList)), JsonSerializable(typeof(EngineHistory))]   // API P13: GET /engine, /engine/instances, /engine/instances/{id}/history (their members are reached through these)


[JsonSerializable(typeof(StatusTimeout))]

[JsonSerializable(typeof(MapImportChallenge))]

[JsonSerializable(typeof(ContextChallenge)), JsonSerializable(typeof(ReviewContextMapRequest)), JsonSerializable(typeof(ReviewContextMapResponse)), JsonSerializable(typeof(PromoteContextMapRequest))]
[JsonSerializable(typeof(PromoteContextMapResponse)), JsonSerializable(typeof(ProposeContextMapRequest)), JsonSerializable(typeof(ProposeContextMapResponse)), JsonSerializable(typeof(ReviewQueueView))]
[JsonSerializable(typeof(ReviewHistoryView)), JsonSerializable(typeof(ReviewExported)), JsonSerializable(typeof(ReviewResult)), JsonSerializable(typeof(ProposalsView))]
[JsonSerializable(typeof(LensesView)), JsonSerializable(typeof(ScopeGraphView)), JsonSerializable(typeof(ScopeGraphCitation)), JsonSerializable(typeof(LensesSetView)), JsonSerializable(typeof(HistoryView)), JsonSerializable(typeof(CodeView)), JsonSerializable(typeof(SubgraphView)), JsonSerializable(typeof(GetContextLensesAnswerRequest)), JsonSerializable(typeof(GetContextHistoryRequest)), JsonSerializable(typeof(GetContextCodeRequest)), JsonSerializable(typeof(GetContextSubgraphRequest)), JsonSerializable(typeof(FileMapView)), JsonSerializable(typeof(WholeMapView)), JsonSerializable(typeof(MapSearchView)), JsonSerializable(typeof(SearchContextMapRequest)), JsonSerializable(typeof(ConceptsView)), JsonSerializable(typeof(ConceptMembersView)), JsonSerializable(typeof(ConceptGlossaryView)), JsonSerializable(typeof(PagesView)), JsonSerializable(typeof(PageRow)), JsonSerializable(typeof(PageActionRow)), JsonSerializable(typeof(PinSuggestView)), JsonSerializable(typeof(PinSuggestTerm)), JsonSerializable(typeof(PinSuggestFeature)), JsonSerializable(typeof(PinSuggestMatch)), JsonSerializable(typeof(ContextPinsView)), JsonSerializable(typeof(ContextPinRow)), JsonSerializable(typeof(ContextPinMoved)), JsonSerializable(typeof(ContextPinMovedTo)), JsonSerializable(typeof(PinMove)), JsonSerializable(typeof(PinMoveTo)), JsonSerializable(typeof(ContextRebuildView)), JsonSerializable(typeof(GetContextPinsRequest)), JsonSerializable(typeof(ContextMapReviewed)), JsonSerializable(typeof(ContextMapPromoted))]
[JsonSerializable(typeof(ContextMapProposed)), JsonSerializable(typeof(ContextMapBuilt))]
[JsonSerializable(typeof(FleetChanged))]
[JsonSerializable(typeof(ItemOpenRequested)), JsonSerializable(typeof(LaunchStarted)), JsonSerializable(typeof(LaunchStartedPublic)), JsonSerializable(typeof(LaunchFailed)), JsonSerializable(typeof(LaunchWaiting)), JsonSerializable(typeof(ModelPicked)), JsonSerializable(typeof(ModelPicking)), JsonSerializable(typeof(PickedFile)), JsonSerializable(typeof(OmittedFile))]   // API P10a (U0)
[JsonSerializable(typeof(LaunchOwnerWaiting))]   // skill flows spec §7: launch.owner-waiting
[JsonSerializable(typeof(LaunchShowReport)), JsonSerializable(typeof(LaunchProbed)), JsonSerializable(typeof(SessionEnded)), JsonSerializable(typeof(SessionUsage)), JsonSerializable(typeof(LaunchSessionReported))]
[JsonSerializable(typeof(Core.Sandbox.Denials.DenialFile)), JsonSerializable(typeof(SandboxDenied)), JsonSerializable(typeof(SandboxLimitHit)), JsonSerializable(typeof(SandboxLearnEnded)), JsonSerializable(typeof(SandboxEntryAllowed)), JsonSerializable(typeof(ReviewLoginSet)), JsonSerializable(typeof(SandboxSummary)), JsonSerializable(typeof(StepPolicy)), JsonSerializable(typeof(ModelLoginSource)), JsonSerializable(typeof(PreparedLogin)), JsonSerializable(typeof(LoginStatus))]   // Sandbox SB-0
[JsonSerializable(typeof(LaunchViewerAttached)), JsonSerializable(typeof(LaunchTakeoverOpened))]   // issue 1004
[JsonSerializable(typeof(GetLaunchTargetsRequest)), JsonSerializable(typeof(LaunchTargets)), JsonSerializable(typeof(ProbeLaunchTargetsRequest)), JsonSerializable(typeof(ProbeLaunchTargetsResult))]   // API P10a (U11): the launch targets and their probe job
[JsonSerializable(typeof(WorkflowChangedPayload))]   // #2258 PR 8: the five workflow lifecycle events
[JsonSerializable(typeof(WorkflowRunRow)), JsonSerializable(typeof(WorkflowRunStartedPayload)), JsonSerializable(typeof(WorkflowRunFinishedPayload)), JsonSerializable(typeof(WorkflowRunCancelledPayload)), JsonSerializable(typeof(WorkflowNodeStartedPayload)), JsonSerializable(typeof(WorkflowNodeAgentChangedPayload)), JsonSerializable(typeof(WorkflowNodeFinishedPayload)), JsonSerializable(typeof(WorkflowNodeFailedPayload)), JsonSerializable(typeof(WorkflowNodeTakenOverPayload)), JsonSerializable(typeof(WorkflowNodeHandedBackPayload))]   // issue 2258 PR 6: the graph runs view and its six events
[JsonSerializable(typeof(ModelHistoryRow)), JsonSerializable(typeof(RunScope)), JsonSerializable(typeof(RunModel)), JsonSerializable(typeof(StateOrder))]   // model-pick M-4: the model-history and model-runs views
[JsonSerializable(typeof(LaunchRow)), JsonSerializable(typeof(LaunchEnd)), JsonSerializable(typeof(LaunchViewer)), JsonSerializable(typeof(LaunchViewerPublic))]   // API P10a (U8): the launches view
[JsonSerializable(typeof(OpenItemRequest)), JsonSerializable(typeof(OpenItemBody)), JsonSerializable(typeof(LaunchRequested)), JsonSerializable(typeof(GetLaunchRequest)), JsonSerializable(typeof(LaunchView))]
[JsonSerializable(typeof(EndLearnModeRequest)), JsonSerializable(typeof(RecordLimitHitRequest)), JsonSerializable(typeof(RecordLimitHitResponse)), JsonSerializable(typeof(LimitHitBody)), JsonSerializable(typeof(GetRunSuspensionRequest)), JsonSerializable(typeof(RunSuspensionView)), JsonSerializable(typeof(CodeIndexRow))]
[JsonSerializable(typeof(ListDenialsRequest)), JsonSerializable(typeof(ListDenialsResponse)), JsonSerializable(typeof(Core.Sandbox.Denials.DenialFile)), JsonSerializable(typeof(Core.Sandbox.Plan.SandboxPlan))]   // SB-4b: the denials read and the plan file `sandbox explain` reads
[JsonSerializable(typeof(AllowDenialRequest)), JsonSerializable(typeof(AllowDenialResponse))]   // SB-6a: the Allow button
[JsonSerializable(typeof(CheckSandboxRequest)), JsonSerializable(typeof(CheckSandboxResponse)), JsonSerializable(typeof(SandboxBlockerLine)), JsonSerializable(typeof(SandboxResumeLine))]   // SB-5a: Check again
[JsonSerializable(typeof(Features.GetSkills.GetSkillsRequest)), JsonSerializable(typeof(Features.GetSkills.GetSkillsResponse)), JsonSerializable(typeof(Features.GetSkills.SkillBlockView)), JsonSerializable(typeof(Features.GetSkills.SkillInputView)), JsonSerializable(typeof(Features.GetSkills.SkillNotLoadedView))]   // F-1: the loaded skill blocks
[JsonSerializable(typeof(Features.GetSandboxStatus.GetSandboxStatusRequest)), JsonSerializable(typeof(Features.GetSandboxStatus.SandboxStatusResponse)), JsonSerializable(typeof(Features.GetSandboxStatus.SandboxLearnLine))]   // SB-6a: the sandbox status
[JsonSerializable(typeof(SessionHeartbeatBody)), JsonSerializable(typeof(SessionHeartbeatResponse)), JsonSerializable(typeof(SessionExitBody)), JsonSerializable(typeof(SessionExitResponse)), JsonSerializable(typeof(CaptureRule))]   // API P10a (U10)
// Two LaunchId types meet here: the collector's looser one (Core.Launch) and the engine's 'LCH-…' one the launch events carry (SYSLIB1031).
[JsonSerializable(typeof(LaunchId), TypeInfoPropertyName = "EngineLaunchId")]
[JsonSerializable(typeof(SyncContextSourcesRequest)), JsonSerializable(typeof(SyncContextSourcesResult)), JsonSerializable(typeof(SyncIndexSourceRequest)), JsonSerializable(typeof(SyncIndexSourceResult)), JsonSerializable(typeof(BuildContextMapRequest)), JsonSerializable(typeof(BuildContextMapResult))]
[JsonSerializable(typeof(RefreshContextMapRequest)), JsonSerializable(typeof(RefreshContextMapResult)), JsonSerializable(typeof(ContextMapRefreshed))]
[JsonSerializable(typeof(BuildCodeIndexRequest)), JsonSerializable(typeof(BuildCodeIndexResult)), JsonSerializable(typeof(EmbedCodeIndexRequest)), JsonSerializable(typeof(EmbedCodeIndexResult)), JsonSerializable(typeof(CodeIndexBuilt)), JsonSerializable(typeof(CodeIndexBuiltLayer))]
[JsonSerializable(typeof(RelinkPinAliasesRequest)), JsonSerializable(typeof(RelinkPinAliasesJobResult)), JsonSerializable(typeof(Context.Core.Linking.PinLinkCount)), JsonSerializable(typeof(ScanContextConceptsRequest)), JsonSerializable(typeof(ScanContextConceptsResult)), JsonSerializable(typeof(PublishContextMapRequest)), JsonSerializable(typeof(PublishContextMapResult)), JsonSerializable(typeof(ContextMapHeld)), JsonSerializable(typeof(ContextMapPublished)), JsonSerializable(typeof(ContextMapPublishedPublic)), JsonSerializable(typeof(ContextMapHoldDropped))]
[JsonSerializable(typeof(Context.Core.Eval.MapQuality.MapQualityRun)), JsonSerializable(typeof(Context.Core.Eval.Backtest.BacktestRun)), JsonSerializable(typeof(Context.Core.Eval.Backtest.EvalPurgeReport))]
[JsonSerializable(typeof(Features.PinConcepts.PinConceptsRequest)), JsonSerializable(typeof(Features.PinConcepts.PinConceptsResult)), JsonSerializable(typeof(Features.PinConcepts.PinSet)), JsonSerializable(typeof(Features.PinConcepts.PinWritten)), JsonSerializable(typeof(Commands.Pins.PinList)), JsonSerializable(typeof(Commands.Pins.PinListEntry))]   // concept pins: the PinConcepts job
[JsonSerializable(typeof(Features.SetPages.SetPagesRequest)), JsonSerializable(typeof(Features.SetPages.SetPagesResult)), JsonSerializable(typeof(Features.SetPages.PageWritten))]   // pages: the SetPages job
[JsonSerializable(typeof(RunContextBacktestRequest)), JsonSerializable(typeof(RunContextBacktestResult)), JsonSerializable(typeof(BacktestArmLine))]
[JsonSerializable(typeof(GetLinearLookupsRequest)), JsonSerializable(typeof(LinearLookupsResponse)), JsonSerializable(typeof(GetLinearSinceRequest)), JsonSerializable(typeof(LinearSinceResponse)), JsonSerializable(typeof(LinearSinceItem))]   // Linear hub Ph3 1a: the lookups and the strip
internal sealed partial class SindriJsonContext : JsonSerializerContext;
