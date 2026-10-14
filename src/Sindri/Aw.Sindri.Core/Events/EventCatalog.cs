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
        new(EventTypes.ProfileApproved, Public: true, typeof(ProfileApproved), "profile.approved:<hash>:after:<seq>") { Invalidates = [ApiViews.Status, ApiViews.Profile, ApiViews.Services] },
        new("service.heartbeat", Public: true, typeof(Heartbeat), "service.heartbeat:<epoch>:<slot>") { Invalidates = [ApiViews.Status] },
        new(EventTypes.ContextSourcesSynced, Public: true, typeof(ContextSourcesSynced), "context.sources.synced:<epoch>:<slot>"),
        new(EventTypes.ContextMapImported, Public: true, typeof(ContextMapImported), "context.map.imported:<hash>:v<version>"),
        new(EventTypes.CommandHandled, Public: false, PublicPayload: null, "command.handled:<commandId>"),
        new(EventTypes.ServiceRan, Public: true, typeof(ServiceRan), "service.ran:<name>:<epoch>:<slot>") { Invalidates = [ApiViews.Status, ApiViews.Services] },
        new(EventTypes.ProfileChecked, Public: true, typeof(ProfileChecked), "profile.checked:<previousCheckSequence>") { Invalidates = [ApiViews.Status, ApiViews.Profile] },
        new(EventTypes.ViewsRebuilt, Public: false, PublicPayload: null, "views.rebuilt:<commandId>"),
        new(EventTypes.WorkflowStarted, Public: true, typeof(WorkflowEventPayload), "workflow:<workflow id>:<run id>:<activity id>"),
        new(EventTypes.WorkflowTransition, Public: true, typeof(WorkflowEventPayload), "workflow:<workflow id>:<run id>:<activity id>"),
        new(EventTypes.ItemCreated, Public: true, typeof(ItemCreatedPublic), "item.created:<itemId>") { Invalidates = [ApiViews.Queue, ApiViews.Items, ApiViews.Item, ApiViews.Timeline] },
        new(EventTypes.ItemStateChanged, Public: true, typeof(ItemStateChanged), "item.state-changed:<itemId>:v<version>") { Invalidates = [ApiViews.Queue, ApiViews.Items, ApiViews.Item, ApiViews.Timeline] },
        new(EventTypes.ItemLinked, Public: true, typeof(ItemLinked), "item.linked:<itemId>:<record>") { Invalidates = [ApiViews.Items, ApiViews.Item, ApiViews.Timeline] },
    ];
}
