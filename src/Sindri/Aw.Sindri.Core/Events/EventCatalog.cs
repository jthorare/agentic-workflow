using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Events;

/// <summary>Every event type Sindri records (API spec §3.4): the phase that adds an event registers it here.</summary>
public static class EventCatalog
{
    /// <summary>Every entry.</summary>
    public static IReadOnlyList<EventCatalogEntry> All { get; } =
    [
        // one row per event type; phases append
        new(EventTypes.ProcessStarted, Public: true, typeof(ProcessStarted), "process.started:<epoch>") { Invalidates = [ApiViews.Status] },
        new(EventTypes.ProcessStopped, Public: true, typeof(ProcessStopped), "process.stopped:<epoch>") { Invalidates = [ApiViews.Status] },
        new(EventTypes.ProfileApproved, Public: true, typeof(ProfileApproved), "profile.approved:<hash>:after:<seq>") { Invalidates = [ApiViews.Status, ApiViews.Profile, ApiViews.Services, ApiViews.NeedsYou] },
        new("service.heartbeat", Public: true, typeof(Heartbeat), "service.heartbeat:<epoch>:<slot>") { Invalidates = [ApiViews.Status] },
        new(EventTypes.ContextSourcesSynced, Public: true, typeof(ContextSourcesSynced), "context.sources.synced:<epoch>:<source>:<slot>"),
        new(EventTypes.ContextMapImported, Public: true, typeof(ContextMapImported), "context.map.imported:<hash>:v<version>"),
        new(EventTypes.ContextMapReviewed, Public: true, typeof(ContextMapReviewed), "context.map.reviewed:<overrideId>"),
        new(EventTypes.ContextMapPromoted, Public: true, typeof(ContextMapPromoted), "context.map.promoted:<repo>:v<version>"),
        new(EventTypes.ContextMapProposed, Public: true, typeof(ContextMapProposed), "context.map.proposed:<run>:<rows hash16>"),
        new(EventTypes.ContextMapBuilt, Public: true, typeof(ContextMapBuilt), "context.map.built:<epoch>:<slot>"),
        new(EventTypes.CommandHandled, Public: false, PublicPayload: null, "command.handled:<commandId>"),
        new(EventTypes.RecipeRegistered, Public: true, typeof(RecipeRegistered), "recipe.registered:<hash>"),
        new(EventTypes.RecipeApproved, Public: true, typeof(RecipeApproved), "recipe.approved:<hash>:after:<seq>"),
        new(EventTypes.RecipeRevoked, Public: true, typeof(RecipeRevoked), "recipe.revoked:<hash>"),
        new(EventTypes.ItemCreated, Public: true, typeof(ItemCreatedPublic), "item.created:<itemId>") { Invalidates = [ApiViews.Queue, ApiViews.Items, ApiViews.Item, ApiViews.Timeline] },
        new(EventTypes.ItemStateChanged, Public: true, typeof(ItemStateChanged), "item.state-changed:<itemId>:v<version>") { Invalidates = [ApiViews.Queue, ApiViews.Items, ApiViews.Item, ApiViews.Timeline, ApiViews.NeedsYou, ApiViews.Requests, ApiViews.Decisions] },
        new(EventTypes.ItemLinked, Public: true, typeof(ItemLinked), "item.linked:<itemId>:<record>") { Invalidates = [ApiViews.Items, ApiViews.Item, ApiViews.Timeline] },
        new(EventTypes.RequestFiled, Public: true, typeof(RequestFiledPublic), "request.filed:<requestId>") { Invalidates = [ApiViews.NeedsYou, ApiViews.Requests, ApiViews.Decisions, ApiViews.Queue, ApiViews.Items, ApiViews.Item, ApiViews.Timeline] },
        new(EventTypes.DecisionRecorded, Public: true, typeof(DecisionRecordedPublic), "decision.recorded:<decisionId>") { Invalidates = [ApiViews.NeedsYou, ApiViews.Requests, ApiViews.Decisions, ApiViews.Queue, ApiViews.Items, ApiViews.Item, ApiViews.Timeline] },
        new(EventTypes.WorkflowStarted, Public: true, typeof(WorkflowEventPayload), "workflow:<workflow id>:<run id>:<activity id>[:<content hash>]"),
        new(EventTypes.WorkflowTransition, Public: true, typeof(WorkflowEventPayload), "workflow:<workflow id>:<run id>:<activity id>[:<content hash>]"),
        new(EventTypes.WorkflowRefused, Public: true, typeof(WorkflowEventPayload), "workflow:<workflow id>:<run id>:<activity id>[:<content hash>]"),
        new(EventTypes.WorkflowShifted, Public: true, typeof(WorkflowEventPayload), "workflow:<workflow id>:<run id>:<activity id>[:<content hash>]"),
        new(EventTypes.WorkflowNotified, Public: true, typeof(WorkflowEventPayload), "workflow:<workflow id>:<run id>:<activity id>[:<content hash>]"),
        new(EventTypes.WorkflowExpired, Public: true, typeof(WorkflowEventPayload), "workflow:<workflow id>:<run id>:<activity id>[:<content hash>]"),
        new(EventTypes.ServiceRan, Public: true, typeof(ServiceRan), "service.ran:<name>:<epoch>:<slot> | service.ran:<name>:manual:<jobId>") { Invalidates = [ApiViews.Status, ApiViews.Services, ApiViews.NeedsYou] },
        new(EventTypes.JobStarted, Public: true, typeof(JobStarted), "job.started:<jobId>") { Invalidates = [ApiViews.Job, ApiViews.Jobs, ApiViews.Services] },
        new(EventTypes.JobProgress, Public: true, typeof(JobProgress), "job.progress:<jobId>:<n>") { Invalidates = [ApiViews.Job, ApiViews.Jobs] },
        new(EventTypes.JobFinished, Public: true, typeof(JobFinishedPublic), "job.finished:<jobId>") { Invalidates = [ApiViews.Job, ApiViews.Jobs, ApiViews.Services] },
        new(EventTypes.JobCancelled, Public: true, typeof(JobCancelled), "job.cancelled:<jobId>") { Invalidates = [ApiViews.Job, ApiViews.Jobs, ApiViews.Services] },
        new(EventTypes.ServicePaused, Public: true, typeof(ServicePaused), "service.paused:<name>:<commandId>") { Invalidates = [ApiViews.Services, ApiViews.Status] },
        new(EventTypes.ServiceResumed, Public: true, typeof(ServiceResumed), "service.resumed:<name>:<commandId>") { Invalidates = [ApiViews.Services, ApiViews.Status] },
        new(EventTypes.ProfileChecked, Public: true, typeof(ProfileChecked), "profile.checked:<previousCheckSequence>") { Invalidates = [ApiViews.Status, ApiViews.Profile, ApiViews.NeedsYou] },
        new(EventTypes.FleetChanged, Public: true, typeof(FleetChanged), "fleet.changed:<provider>:<id>:<change>:<observedAtMs>") { Invalidates = [ApiViews.Status] },
        new(EventTypes.ItemOpenRequested, Public: true, typeof(ItemOpenRequested), "item.open-requested:<launchId>") { Invalidates = [ApiViews.Launch, ApiViews.Item, ApiViews.Items, ApiViews.Timeline] },
        new(EventTypes.LaunchStarted, Public: true, typeof(LaunchStartedPublic), "launch.outcome:<launchId>") { Invalidates = [ApiViews.Launch, ApiViews.Item, ApiViews.Items, ApiViews.Timeline, ApiViews.NeedsYou] },
        new(EventTypes.LaunchFailed, Public: true, typeof(LaunchFailed), "launch.outcome:<launchId>") { Invalidates = [ApiViews.Launch, ApiViews.Item, ApiViews.Items, ApiViews.Timeline, ApiViews.NeedsYou, ApiViews.LaunchTargets] },
        new(EventTypes.LaunchProbed, Public: true, typeof(LaunchProbed), "launch.probed:<probe>") { Invalidates = [ApiViews.LaunchTargets, ApiViews.Item] },
        new(EventTypes.SessionEnded, Public: true, typeof(SessionEnded), "session.ended:<launch>") { Invalidates = [ApiViews.Launch, ApiViews.Item, ApiViews.Items, ApiViews.Timeline] },
        new(EventTypes.SessionUsage, Public: false, PublicPayload: null, "session.usage:<launch>:<n>"),
        new(EventTypes.LaunchSessionReported, Public: true, typeof(LaunchSessionReported), "launch.session-reported:<launch>") { Invalidates = [ApiViews.Launch, ApiViews.Item] },
        new(EventTypes.LaunchViewerAttached, Public: true, typeof(LaunchViewerAttached), "launch.viewer-attached:<launch>") { Invalidates = [ApiViews.Launch, ApiViews.Item] },
        new(EventTypes.ViewsRebuilt, Public: false, PublicPayload: null, "views.rebuilt:<commandId>"),
    ];
}
