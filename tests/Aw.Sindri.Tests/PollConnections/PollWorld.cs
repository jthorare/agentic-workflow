using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.PollConnections;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Connections;

namespace Aw.Sindri.Tests.PollConnections;

/// <summary>The ports the poll service reads and writes, in memory, with a scripted provider, and the service built on them.</summary>
internal sealed class PollWorld
{
    public const string ConnectionText = "linear:work";
    public const string KindText = "linear.issue.labeled";

    public PollWorld()
    {
        Workflows = new InMemoryWorkflowStore(Connections.Clock);
        Runs = new InMemoryWorkflowRunStore(Connections.Clock);
        Starter = new RecordingStarter(Runs, Connections.Clock);
    }

    private int _created;

    public ConnectionHarness Connections { get; } = new();

    public InMemoryWorkflowStore Workflows { get; }

    public InMemoryWorkflowRunStore Runs { get; }

    public RecordingStarter Starter { get; }

    public InMemoryPollCursors Cursors { get; } = new();

    public PollWake Wake { get; } = new();

    public RecentEvents Recent { get; } = new();

    public static ConnectionName Name { get; } = ConnectionSamples.Name();

    public static EventKind Kind { get; } = new(KindText);

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public async Task Connect()
    {
        _ = await Connections.Store.Upsert(ConnectionSamples.Oauth(Name), Token);
        await Connections.Hold();
    }

    /// <summary>Saves and activates a workflow whose trigger is a connection_event on the harness connection and event.</summary>
    public async Task<WorkflowSlug> GoLive(string slug, string? filter = null, string? concurrency = null)
    {
        var config = new JsonObject { ["connection"] = ConnectionText, ["event"] = KindText };
        if (filter is not null)
        {
            config["filter"] = filter;
        }

        var id = WorkflowId.From("WF-" + (++_created).ToString("D32", System.Globalization.CultureInfo.InvariantCulture));
        var spec = new GraphSpec(1, slug, Description: null, concurrency, Resources: null, [new GraphNode(NodeId.From("start"), "connection_event", Title: null, config)], []);
        _ = (await Workflows.Create(id, WorkflowSlug.From(slug), spec, "owner", Token)).Value;
        _ = (await Workflows.Activate(id, 1, Token)).Value;
        return WorkflowSlug.From(slug);
    }

    /// <summary>Archives the workflow with this slug: it leaves the list and its trigger stops watching.</summary>
    public async Task Archive(string slug)
    {
        var record = (await Workflows.List(includeArchived: false, Token)).Value.Single(r => r.Slug.Value == slug);
        _ = (await Workflows.Archive(record.Id, Token)).Value;
    }

    public static PolledEvent Event(string key, params (string Name, string Value)[] fields) =>
        new(key, Kind, KnownInstants.Epoch, fields.ToDictionary(static f => f.Name, static f => f.Value, StringComparer.Ordinal));

    public static Result<PollResult> Page(string? next, params PolledEvent[] events) => new PollResult(events, next, RateLimited: false, RetryAfter: null);

    public Func<string, int>? Occurrence { get; set; }

    public PollRound Round() => new(Starter, Cursors, Connections.Events(), Connections.Clock, Runs, Recent, Occurrence);

    public CatchUp CatchUp() => new(Round(), Connections.Events());

    public PollConnectionsService Service(IEventPoller poller) =>
        new(new PollGroupSource(Workflows), new GroupReader(Connections.Store, [poller], CatchUp()), Cursors, Wake, new NoProfile(), Connections.Clock);

    public async Task<Result<ScheduledResult>> Tick(IEventPoller poller) =>
        await Service(poller).Run(await Connections.Tick(), Token);

    public IEnumerable<string> Recorded() => Connections.Ledger.Events.Select(static e => e.Envelope.Type);

    private sealed class NoProfile : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult<Result<LoadedProfile?>>((LoadedProfile?)null);
    }

    /// <summary>Records each request and, like the real starter, stores the run under its id so a delivery that has its run is refused the second time.</summary>
    internal sealed class RecordingStarter(InMemoryWorkflowRunStore runs, TimeProvider clock) : IGraphRunStarter
    {
        public List<GraphStartRequest> Requests { get; } = [];

        public bool Unreachable { get; set; }

        public bool Skip { get; set; }

        /// <summary>When true, the next start records its run and then fails its launch, as the real starter does for a launch error other than Temporal down (the run stays, failed).</summary>
        public bool FailNextLaunch { get; set; }

        public async ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken)
        {
            var id = WorkflowRunId.Of(request.Slug, request.Revision, request.Occurrence);
            if (Unreachable)
            {
                return SindriErrors.EngineUnavailable.With("the engine is down");
            }

            if (Skip)
            {
                return new GraphStart(GraphStartKind.Skipped, id);
            }

            var inserted = await runs.Insert(new WorkflowRun(id, request.Workflow, request.Revision, request.Trigger, WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, clock.GetUtcNow(), EndedAt: null), cancellationToken);
            if (inserted.IsError)
            {
                return inserted.Error;
            }

            if (FailNextLaunch)
            {
                FailNextLaunch = false;
                return SindriErrors.InvalidQuery.With("the workflow refused its input");
            }

            Requests.Add(request);
            return new GraphStart(GraphStartKind.Started, id);
        }
    }
}
