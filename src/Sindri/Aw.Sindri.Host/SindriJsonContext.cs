using System.Text.Json.Serialization;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.CloseItem;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Features.GetItem;
using Aw.Sindri.Features.GetItemTimeline;
using Aw.Sindri.Features.GetProfile;
using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.ListCommands;
using Aw.Sindri.Features.ListItems;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Features.RebuildViews;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Features.ValidateProfile;
using Aw.Sindri.Features.ValidateRecipe;
using Aw.Sindri.Host.Commands.Recipe;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host;

/// <summary>The package's one source-generated JSON context (spec §4b.3): through AwJson.CreateOptions, and an http host's resolver after ASP.NET's ProblemDetails context.</summary>
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(SindriProfile))]
[JsonSerializable(typeof(ControlEndpoint))]
[JsonSerializable(typeof(Ping))]
[JsonSerializable(typeof(ApiProblem))]
[JsonSerializable(typeof(ProcessStarted))]
[JsonSerializable(typeof(ProcessStopped))]
[JsonSerializable(typeof(WorkflowEventPayload))]
[JsonSerializable(typeof(ProfileApproved))]
[JsonSerializable(typeof(Heartbeat))]
[JsonSerializable(typeof(CommandHandled))]
[JsonSerializable(typeof(ServiceRan))]
[JsonSerializable(typeof(ProfileChecked))]
[JsonSerializable(typeof(ViewsRebuilt))]
[JsonSerializable(typeof(ItemCreated))]
[JsonSerializable(typeof(ItemCreatedPublic))]
[JsonSerializable(typeof(ItemStateChanged))]
[JsonSerializable(typeof(ItemLinked))]
[JsonSerializable(typeof(AsOf))]
[JsonSerializable(typeof(ViewsStatus))]
[JsonSerializable(typeof(ServiceRow))]
[JsonSerializable(typeof(ProfileRow))]
[JsonSerializable(typeof(ItemRow))]
[JsonSerializable(typeof(AliasRow))]
[JsonSerializable(typeof(TimelineRow))]
[JsonSerializable(typeof(HealthReport))]
[JsonSerializable(typeof(CommandAccepted))]
[JsonSerializable(typeof(GetStatusResponse))]
[JsonSerializable(typeof(StopProcessResponse))]
[JsonSerializable(typeof(ApproveProfileRequest))]
[JsonSerializable(typeof(ApproveProfileResponse))]
[JsonSerializable(typeof(ValidateProfileResponse))]
[JsonSerializable(typeof(RunDoctorResponse))]
[JsonSerializable(typeof(PublicEvent))]
[JsonSerializable(typeof(StreamErrorFrame))]
[JsonSerializable(typeof(ListCommandsRequest))]
[JsonSerializable(typeof(ListCommandsResponse))]
[JsonSerializable(typeof(CommandListRow))]
[JsonSerializable(typeof(ListServicesRequest))]
[JsonSerializable(typeof(ListServicesResponse))]
[JsonSerializable(typeof(ServiceView))]
[JsonSerializable(typeof(GetProfileRequest))]
[JsonSerializable(typeof(GetProfileResponse))]
[JsonSerializable(typeof(RebuildViewsRequest))]
[JsonSerializable(typeof(CreateItemRequest))]
[JsonSerializable(typeof(ItemCreatedResponse))]
[JsonSerializable(typeof(CloseItemRequest))]
[JsonSerializable(typeof(CloseItemBody))]
[JsonSerializable(typeof(GetQueueRequest))]
[JsonSerializable(typeof(QueueResponse))]
[JsonSerializable(typeof(QueueGroupView))]
[JsonSerializable(typeof(ItemSummary))]
[JsonSerializable(typeof(ListItemsRequest))]
[JsonSerializable(typeof(ListItemsResponse))]
[JsonSerializable(typeof(GetItemRequest))]
[JsonSerializable(typeof(ItemView))]
[JsonSerializable(typeof(GetItemTimelineRequest))]
[JsonSerializable(typeof(TimelineResponse))]
[JsonSerializable(typeof(Recipe))]
[JsonSerializable(typeof(BlockDescriptor))]
[JsonSerializable(typeof(ValidateRecipeResponse))]
[JsonSerializable(typeof(EvalRecipeResponse))]
[JsonSerializable(typeof(WorkflowEvent))]
[JsonSerializable(typeof(EvalStartLine))]
[JsonSerializable(typeof(EvalStepLine))]
[JsonSerializable(typeof(RecipeValidationView))]
[JsonSerializable(typeof(ContextResult))]
[JsonSerializable(typeof(SourcesView))]
[JsonSerializable(typeof(ContextSourcesSynced))]
[JsonSerializable(typeof(ContextMapImported))]
[JsonSerializable(typeof(MapDocument))]
[JsonSerializable(typeof(MapStatusView))]
[JsonSerializable(typeof(MapExported))]
[JsonSerializable(typeof(MapHealth))]
[JsonSerializable(typeof(PurgeReport))]
[JsonSerializable(typeof(ImportContextMapRequest))]
[JsonSerializable(typeof(ImportContextMapResponse))]
// Two RecordKey types meet here: Sindri's item link key and the context lane's source key; name Sindri's apart (SYSLIB1031).
[JsonSerializable(typeof(Core.Ids.RecordKey), TypeInfoPropertyName = "ItemRecordKey")]
internal sealed partial class SindriJsonContext : JsonSerializerContext;
