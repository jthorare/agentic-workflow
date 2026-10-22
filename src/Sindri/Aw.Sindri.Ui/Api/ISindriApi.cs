using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Api.Jobs;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>Sindri's HTTP API as the app uses it (docs/csharp/sindri.md). Every call names the base URL from control.json.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/ping.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answering process, or SND-UI-002/003.</returns>
    public Task<Result<PingView>> Ping(Uri api, CancellationToken cancellationToken);

    /// <summary>GET /sindri/status.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The status, or SND-UI-002/003.</returns>
    public Task<Result<StatusView>> Status(Uri api, CancellationToken cancellationToken);

    /// <summary>GET /sindri/events?after=N: the ledger's events after a cursor, as they happen. Ends when the connection does.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="after">The last sequence already seen (0 for all).</param>
    /// <param name="cancellationToken">Ends the stream.</param>
    /// <returns>The events, in order.</returns>
    public IAsyncEnumerable<EventView> Events(Uri api, long after, CancellationToken cancellationToken);

    /// <summary>
    /// GET /sindri/events?types=workflow.instance-changed[&amp;instance=ADDRESS]: the frames that say an instance's view changed. The first item says the
    /// stream is open; it opens at the ledger head, so a change while the app was away is lost and the caller re-reads what it shows once it is open.
    /// The frames carry no id: they never move the cursor of <see cref="Events"/>. Ends when the connection does.
    /// </summary>
    /// <param name="api">The base URL.</param>
    /// <param name="instance">One instance's address, or null for every instance.</param>
    /// <param name="cancellationToken">Ends the stream.</param>
    /// <returns>The open signal, then the changes.</returns>
    public IAsyncEnumerable<InstanceStreamItem> InstanceChanges(Uri api, string? instance, CancellationToken cancellationToken);

    /// <summary>GET /sindri/commands: the command catalog (read on connect and on a new epoch, UI spec §9.1).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The catalog, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<CommandList>> Commands(Uri api, CancellationToken cancellationToken);

    /// <summary>GET /sindri/services (API P2): U1 reads its EmptyText sentence and asOf for the stalled and rebuilding banners.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ServiceList>> Services(Uri api, CancellationToken cancellationToken);

    /// <summary>POST /sindri/views/rebuild (RebuildViews, desk, API P2): sent once, never retried by the app (UI spec §9.1).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted command, the API's refusal (title and detail as served), or SND-UI-002/003.</returns>
    public Task<Result<CommandAcceptedView>> RebuildViews(Uri api, CancellationToken cancellationToken);

    /// <summary>GET /sindri/queue (API P3): every group in its fixed order, with its EmptyText sentence (E-18).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<QueueView>> Queue(Uri api, CancellationToken cancellationToken);

    /// <summary>GET /sindri/items?state= (API P3): the items in one state; the API's refusal, or SND-UI-002/003.</summary>
    public Task<Result<ItemListView>> ListItems(Uri api, string state, CancellationToken cancellationToken);

    /// <summary>GET /sindri/items/{itemId} (API P3).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="itemId">The item's id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The item, SND-ITEM-404, another refusal, or SND-UI-002/003.</returns>
    public Task<Result<ItemView>> Item(Uri api, string itemId, CancellationToken cancellationToken);

    /// <summary>GET /sindri/items/{itemId}/timeline?after=N (API P3): one page.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="itemId">The item's id.</param>
    /// <param name="after">The page's cursor (0 for the first, then the previous page's 'next').</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<TimelineView>> Timeline(Uri api, string itemId, long after, CancellationToken cancellationToken);

    /// <summary>POST /sindri/items (CreateItem, desk, API P3): sent once, never retried by the app.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The form.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new id with the accepted command, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ItemCreatedView>> CreateItem(Uri api, CreateItemRequest request, CancellationToken cancellationToken);

    /// <summary>POST /sindri/items/{itemId}/close (CloseItem, desk, API P3): with the version the human saw; sent once.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="itemId">The item's id.</param>
    /// <param name="request">The reason and the version.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted command, SND-ITEM-409 when the item changed, another refusal, or SND-UI-002/003.</returns>
    public Task<Result<CommandAcceptedView>> CloseItem(Uri api, string itemId, CloseItemRequest request, CancellationToken cancellationToken);
    /// <summary>GET /sindri/workflow/instances (API §3.8, E-20): the instance rows and the root-wide counts, narrowed by the query.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="query">What to ask for: root, open, recipe, subject or one address.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<InstanceList>> Instances(Uri api, InstanceQuery query, CancellationToken cancellationToken);

    /// <summary>GET /sindri/workflow/instances/{id} (API §3.8, Q-API-2): one instance by its opaque id. An address is never a path segment: resolve it with <see cref="Instances"/>.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The instance id (WFI-…) as served.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The instance, the API's refusal (SND-ITEM-404 and the like), or SND-UI-002/003.</returns>
    public Task<Result<InstanceDetail>> Instance(Uri api, string id, CancellationToken cancellationToken);

    /// <summary>GET /sindri/workflow/instances/{id}/facts/{key}: the body behind one fact row, capped by the API (truncated says so).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The instance id.</param>
    /// <param name="key">The fact row's key in its fact: form.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The fact, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<FactView>> InstanceFact(Uri api, string id, string key, CancellationToken cancellationToken);

    /// <summary>GET /sindri/workflow/recipes/{id}[@{hash}] (API §3.8, E-22): what a recipe declares.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="recipe">The recipe id.</param>
    /// <param name="hash">The approved hash, or null for the approved one.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The recipe, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<RecipeView>> Recipe(Uri api, string recipe, string? hash, CancellationToken cancellationToken);

    /// <summary>GET /sindri/needs-you (API P4): what waits on the human, in the API's order.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<NeedsYouList>> NeedsYou(Uri api, CancellationToken cancellationToken);

    /// <summary>GET /sindri/decisions?pending=&amp;item= (API P4).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="pending">True for the pending ones, false for the decided and voided, null for all.</param>
    /// <param name="item">Only the decisions of this item, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<DecisionList>> Decisions(Uri api, bool? pending, string? item, CancellationToken cancellationToken);

    /// <summary>GET /sindri/requests?state=&amp;item= (API P4).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="state">Only requests in this state (open, accepted, declined, voided), or null.</param>
    /// <param name="item">Only the requests of this item, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<RequestList>> Requests(Uri api, string? state, string? item, CancellationToken cancellationToken);

    /// <summary>POST /sindri/decisions/{decisionId} (Decide, desk, API P4): sent once, never retried by the app.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="decisionId">The decision.</param>
    /// <param name="request">The choice and the note.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted command, SND-DEC-001/404 or SND-VIEW-001 as served, or SND-UI-002/003.</returns>
    public Task<Result<CommandAcceptedView>> Decide(Uri api, string decisionId, DecideRequest request, CancellationToken cancellationToken);

    /// <summary>POST /sindri/requests (FileRequest, desk, API P4): sent once, never retried by the app.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The kind, item and text.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new ids with the accepted command, SND-REQ-001/002 or an item refusal as served, or SND-UI-002/003.</returns>
    public Task<Result<RequestFiledView>> FileRequest(Uri api, FileRequestRequest request, CancellationToken cancellationToken);

    // LANE ANCHOR U3': the Needs you, decision and request reads and the Decide and FileRequest sends, one method each (the client's are in SindriApiClient.NeedsYou.cs). Insert immediately above this marker, never between the markers.
    // LANE ANCHOR U8: the command sends (nonce, commands, complete), once API P11 serves their shapes (Q-API-7/8); none is declared before. Insert immediately below this marker, never between the markers.

    /// <summary>GET /sindri/workflow/recipes (API §3.8): every registered recipe with its standing. The Start form uses it only to pick one.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<RecipeList>> Recipes(Uri api, CancellationToken cancellationToken);

    /// <summary>POST /sindri/workflow/instances (StartInstance, desk, engine P6): sent once, never retried by the app, and it takes no nonce.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The recipe, the item and the input facts.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The started (or existing) instance, the API's refusal (SND-WF-012, SND-WF-014, SND-RECIPE-001/005/006, SND-CLI-003) as served, or SND-UI-002/003.</returns>
    public Task<Result<StartedView>> StartInstance(Uri api, StartInstanceRequest request, CancellationToken cancellationToken);

    // LANE ANCHOR U9: the context reads and the review send, one method each (the client's are in SindriApiClient.Context.cs, the routes in ContextRoutes); U9e adds Subgraph. Insert immediately below this marker, never between the markers.

    /// <summary>GET /sindri/context/lenses (context spec §8): the three bands around an anchor or a set of them. A read of the context database, so it never waits on the views.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="query">The anchors, depth, draft flag and pinned map version.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The lenses, the API's refusal (SND-CONTEXT-003, -006, -037, -044, -120), or SND-UI-002/003.</returns>
    public Task<Result<LensesView>> Lenses(Uri api, LensesQuery query, CancellationToken cancellationToken);

    /// <summary>GET /sindri/context/filemap: one directory of a repo with its status marks.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="query">The repo, the directory and the flags.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The directory, the API's refusal (SND-CONTEXT-044, -045), or SND-UI-002/003.</returns>
    public Task<Result<FileMapView>> FileMap(Uri api, FileMapQuery query, CancellationToken cancellationToken);

    /// <summary>GET /sindri/context/map/review: the draft's undecided entries.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="limit">How many entries at most, or null for the API's default.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The queue, SND-CONTEXT-044 when there is no draft, or SND-UI-002/003.</returns>
    public Task<Result<ReviewQueueView>> ReviewQueue(Uri api, int? limit, CancellationToken cancellationToken);

    /// <summary>POST /sindri/context/map/review (a desk verb): sent once, never retried by the app. The protected verbs are never sent.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The verb and what it acts on.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What was written, the API's refusal (SND-CONTEXT-047 for a stale row, SND-AUTH-002), or SND-UI-002/003.</returns>
    public Task<Result<ReviewDecisionResponse>> ReviewDecision(Uri api, ReviewDecisionRequest request, CancellationToken cancellationToken);

    /// <summary>GET /sindri/context/map/review/history: the overrides written, newest first.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="limit">How many entries at most, or null for the API's default.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The history, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ReviewHistoryView>> ReviewHistory(Uri api, int? limit, CancellationToken cancellationToken);

    /// <summary>GET /sindri/context/map: the map's version, counts and coverage.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="anchor">An anchor to scope the coverage to, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The status, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<MapStatusView>> MapStatus(Uri api, string? anchor, CancellationToken cancellationToken);

    /// <summary>GET /sindri/context/node?key=: one node with its aliases and edges.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="key">The node's key (a query value, never a path).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The node, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<NodeView>> Node(Uri api, string key, CancellationToken cancellationToken);

    /// <summary>GET /sindri/context/sources: each source's state (no golden yet).</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The sources, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<SourcesView>> Sources(Uri api, CancellationToken cancellationToken);

    // LANE ANCHOR U11a: the Agents page's reads, one method each (the client's are in SindriApiClient.Sessions.cs); the Focus send (U11a-f) is declared once CO-4a serves its goldens. Insert immediately below this marker, never between the markers.

    /// <summary>GET /sindri/sessions (collector spec CA-1): the live agent sessions, the recently ended ones, the collector's problems and the head counts. A session problem (SND-SESSION-002) is a refusal, not a transport failure.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="query">What to ask for; none of it when the page filters in the app.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<SessionList>> Sessions(Uri api, SessionQuery query, CancellationToken cancellationToken);

    /// <summary>GET /sindri/sessions/{provider}/{id} (collector spec CA-2): one live session with its cwd, socket and state history.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="provider">The provider's id as served ("claude-code").</param>
    /// <param name="id">The session id as served.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The session, SND-SESSION-404 when it is unknown or ended, another refusal, or SND-UI-002/003.</returns>
    public Task<Result<SessionView>> Session(Uri api, string provider, string id, CancellationToken cancellationToken);

    // LANE ANCHOR L1: the agent run's reads and the Attach send (the client's are in SindriApiClient.Launch.cs). Insert immediately below this marker, never between the markers.

    /// <summary>GET /sindri/launches/{id} (API P10a): one launch with its state, viewer and attach command.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="launch">The launch id (LCH-…).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The launch, the API's refusal (SND-LAUNCH-404, SND-CLI-003), or SND-UI-002/003.</returns>
    public Task<Result<LaunchView>> Launch(Uri api, string launch, CancellationToken cancellationToken);

    /// <summary>POST /sindri/items/{itemId}/open (OpenItem, desk, API P10a): sent once, never retried by the app. The server opens the viewer; the app execs nothing.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="itemId">The item.</param>
    /// <param name="request">The mode and the version the app read.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted command with its launch, the API's refusal (SND-LAUNCH-001/002/006/008/009, SND-ITEM-409, SND-AUTH-002), or SND-UI-002/003.</returns>
    public Task<Result<OpenAcceptedView>> OpenItem(Uri api, string itemId, OpenItemRequest request, CancellationToken cancellationToken);

    /// <summary>GET /sindri/workflow/instances/{id}/events?after=N: the instance's events, oldest first.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The instance id (WFI-…).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The events, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<InstanceEventsView>> InstanceEvents(Uri api, string id, CancellationToken cancellationToken);

    /// <summary>GET /sindri/jobs?state=&amp;kind= (API P6): the jobs, newest first. A view read: it waits on the views.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="query">What to ask for; none of it when the page filters in the app.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list, the API's refusal (SND-CLI-003 for an unknown kind or state), or SND-UI-002/003.</returns>
    public Task<Result<JobList>> Jobs(Uri api, JobQuery query, CancellationToken cancellationToken);

    /// <summary>GET /sindri/jobs/{id} (API P6): one job with its progress and result.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The job (JOB-…).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The job, SND-JOB-404, another refusal, or SND-UI-002/003.</returns>
    public Task<Result<JobView>> Job(Uri api, string id, CancellationToken cancellationToken);

    /// <summary>POST /sindri/jobs/{id}/cancel (CancelJob, job class, API P6): sent once, never retried by the app.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="id">The job (JOB-…).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The accepted command, SND-JOB-404/409 or SND-AUTH-002 as served, or SND-UI-002/003.</returns>
    public Task<Result<CommandAcceptedView>> CancelJob(Uri api, string id, CancellationToken cancellationToken);
}
