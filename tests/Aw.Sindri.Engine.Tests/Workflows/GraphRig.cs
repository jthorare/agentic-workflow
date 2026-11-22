using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A worker for <see cref="GraphWorkflow"/> on the time-skipping server, over in-memory stores: the real starter and the Temporal launcher start runs,
/// the real activities run them, and the test blocks ('echo', 'fail', 'hold', plus the shipped 'manual' and 'condition') are the nodes.
/// </summary>
internal sealed partial class GraphRig : IAsyncDisposable
{
    private static int _count;
    private static readonly TimeSpan _cancelHangLimit = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(30);
    private readonly string _taskQueue;
    private readonly CancellationTokenSource _stop = new();
    private readonly TemporalWorker _worker;
    private readonly Task _loop;
    private readonly RigLog _log;
    private readonly GraphRunStarter _starter;
    private readonly InMemorySindriStore _ledger;
    private readonly SindriTimeSkippingServer _server;

    private GraphRig(SindriTimeSkippingServer server, RigLog log, IAgentNodeRuntime? agents, ForEachLimits? forEachLimits, int agentLaunchAttempts = 0)
    {
        _server = server;
        _log = log;
        var environment = server.Environment;
        // why: a run id or task queue can be used once on a server, and the counter keeps the slugs of one process apart
        var number = Interlocked.Increment(ref _count).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Slug = $"walk-test-{number}";
        _taskQueue = $"graph-test-{number}";
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        Runs = new InMemoryWorkflowRunStore(clock);
        RunFaults = new FaultingRunStore(Runs);
        Workflows = new InMemoryWorkflowStore(clock);
        Blocks = new GraphTestBlocks();
        Client = environment.Client;
        Questions = new GraphTestQuestions();
        var probe = new FakeProcessProbe();
        _ledger = new InMemorySindriStore(clock, probe);
        var held = new HeldLease();
        held.Hold(_ledger.Acquire(probe.Self(), Token).AsTask().GetAwaiter().GetResult().Value);
        var ids = new SequentialIdGenerator();
        var mirror = new GraphMirror(_ledger, held, new EventFactory(EventScope.ControlPlane, clock, ids, SliceHarness.Json, new AsyncLocalCallerContextAccessor(ids)));
        var launcher = new TemporalGraphRunLauncher(_ => ValueTask.FromResult(Results.Success(Client)), _taskQueue, agentLaunchAttempts);
        var starter = _starter = new GraphRunStarter(RunFaults, launcher, mirror, clock, NullLogger<GraphRunStarter>.Instance);
        // why: the real wait polls on a clock; the fake one never moves while a node waits, so the caller gets the system clock
        var caller = new GraphWorkflowCaller(Workflows, Runs, starter, new InMemoryProfiles(), TimeProvider.System);
        Seen = new GraphTestBlocks.MemorySeen();
        var registry = new GraphBlockRegistry([new ManualBlock(), new ConditionBlock(), new ForEachBlock(forEachLimits is { } limits ? new FixedForEachLimits(limits) : new DefaultForEachLimits()), new CollectBlock(), new OnlyNewBlock(GraphFence.Seen(Seen)), new RunWorkflowBlock(caller), new WorkflowCallTrigger(), new WaitBlock(clock), new AskOwnerBlock(GraphFence.Question(Questions), clock), new NotifyBlock(GraphFence.Notify(Ports)), new CreateItemBlock(GraphFence.CreateItem(Ports)), .. Blocks.All()]);
        var options = new TemporalWorkerOptions(_taskQueue) { LoggerFactory = _log }
            .AddWorkflow<GraphWorkflow>()
            .AddWorkflow<GraphIterationWorkflow>()
            .AddAllActivities(new GraphRunActivities(RunFaults, Workflows, starter, mirror, new GraphRunItemRepair(ItemRepair, NullLogger<GraphRunItemRepair>.Instance)))
            .AddAllActivities(new GraphBlockActivities(registry, Runs, Workflows));
        if (agents is not null)
        {
            _ = options.AddAllActivities(new AgentNodeActivities(agents));
        }

        Control = new TemporalAgentNodeControl(_ => ValueTask.FromResult(Results.Success(Client)));
        _worker = new TemporalWorker(Client, options);
        _loop = _worker.ExecuteAsync(_stop.Token);
    }

    public InMemoryWorkflowRunStore Runs { get; }

    /// <summary>The store the starter and the activities write through: it is <see cref="Runs"/> until a test sets <see cref="FaultingRunStore.FailWhen"/> (issue 2999). Tests read through <see cref="Runs"/>.</summary>
    internal FaultingRunStore RunFaults { get; }

    /// <summary>The run-item repair the activities call when a run ends; it records the runs it was asked about (issue 2588).</summary>
    public SpyRunItemRepair ItemRepair { get; } = new();

    public InMemoryWorkflowStore Workflows { get; }

    public string Slug { get; }

    public GraphTestBlocks Blocks { get; }

    /// <summary>What the ask_owner blocks asked.</summary>
    public GraphTestQuestions Questions { get; }
    /// <summary>What the real 'only_new' block remembers, in memory.</summary>
    public GraphTestBlocks.MemorySeen Seen { get; }

    public ITemporalClient Client { get; }

    /// <summary>The rig's own time-skipping server: a task a stopped rig leaves in flight dies with it and can't freeze time for another rig (issue 2574).</summary>
    public WorkflowEnvironment Environment => _server.Environment;

    /// <summary>The takeover, handback and report of the run's agent node, over the real Temporal adapter.</summary>
    internal TemporalAgentNodeControl Control { get; }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Starts a time-skipping server of its own and a rig on it.</summary>
    /// <param name="agents">The agent runtime of 'run_agent' nodes, when the test has one.</param>
    /// <returns>The started rig.</returns>
    public static Task<GraphRig> StartAsync(IAgentNodeRuntime? agents = null) => StartAsync(agents, forEachLimits: null);

    /// <summary>Starts a time-skipping server of its own and a rig on it whose 'run_agent' launch is tried <paramref name="agentLaunchAttempts"/> times before the node fails.</summary>
    /// <param name="agents">The agent runtime of 'run_agent' nodes.</param>
    /// <param name="agentLaunchAttempts">The cap on launch attempts.</param>
    /// <returns>The started rig.</returns>
    public static Task<GraphRig> StartAsync(IAgentNodeRuntime agents, int agentLaunchAttempts) => StartAsync(agents, forEachLimits: null, agentLaunchAttempts);

    /// <summary>A rig whose for_each blocks read <paramref name="forEachLimits"/> as the profile's limits.</summary>
    /// <param name="forEachLimits">The profile's item cap and batch size.</param>
    /// <returns>The started rig.</returns>
    public static Task<GraphRig> StartAsync(ForEachLimits forEachLimits) => StartAsync(agents: null, forEachLimits);

    private static async Task<GraphRig> StartAsync(IAgentNodeRuntime? agents, ForEachLimits? forEachLimits, int agentLaunchAttempts = 0)
    {
        // why: the SDK core's own warnings (e.g. 'Error while completing workflow activation ... ACTIVITY_UNKNOWN') go to the runtime of the server's client, so the rig's log is that runtime's logger (issue 2877)
        var log = new RigLog();
        var server = new SindriTimeSkippingServer { CoreLogger = log.CreateLogger("temporal-core") };
        await server.InitializeAsync();
        try
        {
            return new GraphRig(server, log, agents, forEachLimits, agentLaunchAttempts);
        }
        catch
        {
            await server.DisposeAsync();
            throw;
        }
    }

    private sealed class FixedForEachLimits(ForEachLimits limits) : IForEachLimitSource
    {
        public ValueTask<ForEachLimits> Current(CancellationToken cancellationToken) => ValueTask.FromResult(limits);
    }

    public static GraphNode Node(string id, string type, string? onFailure = null, JsonObject? config = null) =>
        new(NodeId.From(id), type, Title: null, config ?? [], onFailure);

    public static GraphEdge Edge(string from, string to, string? output = null) => new(NodeId.From(from), output, NodeId.From(to));

    public static GraphSpec Spec(string concurrency, IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges) =>
        new(Version: 1, "Walk test", Description: null, concurrency, Resources: null, nodes, edges);

    /// <summary>Saves the spec as revision 1 of a new workflow.</summary>
    public async Task<(WorkflowId Id, WorkflowSlug Slug)> Define(GraphSpec spec)
    {
        var id = WorkflowId.From("WF-00000000000000000000000000000001");
        var slug = WorkflowSlug.From(Slug);
        _ = (await Workflows.Create(id, slug, spec, "owner", Token)).Value;
        return (id, slug);
    }

    /// <summary>Saves the spec as the active revision 1 of a second workflow that another workflow's 'run_workflow' can call.</summary>
    public async Task<string> DefineCallee(GraphSpec spec)
    {
        var id = WorkflowId.From("WF-00000000000000000000000000000002");
        var slug = $"{Slug}-callee";
        _ = (await Workflows.Create(id, WorkflowSlug.From(slug), spec, "owner", Token)).Value;
        _ = (await Workflows.Activate(id, 1, Token)).Value;
        return slug;
    }

    /// <summary>One node's stored output as JSON, from the first execution of the node.</summary>
    public async Task<JsonNode?> NodeOutput(WorkflowRunId id, string node)
    {
        var rows = (await Runs.NodeRuns(id, Token)).Value;
        return rows.First(n => string.Equals(n.NodeId.Value, node, StringComparison.Ordinal) && n.ItemIndex == 0).Output is { } text ? JsonNode.Parse(text) : null;
    }

    /// <summary>One node's stored input (its interpolated config) as JSON.</summary>
    public async Task<JsonNode?> NodeInput(WorkflowRunId id, string node)
    {
        var rows = (await Runs.NodeRuns(id, Token)).Value;
        return rows.First(n => string.Equals(n.NodeId.Value, node, StringComparison.Ordinal) && n.ItemIndex == 0).Input is { } text ? JsonNode.Parse(text) : null;
    }

    /// <summary>Asks the starter for a run of revision 1, as a trigger does.</summary>
    public async Task<GraphStart> Trigger((WorkflowId Id, WorkflowSlug Slug) workflow, int occurrence, GraphConcurrency mode, string trigger = """{"kind":"manual"}""") =>
        (await _starter.Start(new GraphStartRequest(workflow.Id, workflow.Slug, Revision: 1, occurrence, trigger, mode), Token)).Value;

    /// <summary>
    /// Cancels the run through its Temporal handle, then polls the run row until it has a final status, under the hang limit (5 min)
    /// and not the check patience, so box load cannot fail the wait and only a real hang does; returns the final run row (issue 2678).
    /// </summary>
    /// <param name="id">The run.</param>
    /// <param name="whenParked">
    /// True (the default) sends the cancel only once the run is parked: its history ends at a completed workflow task or a started timer (check patience, else it throws).
    /// False sends it at once, for a test that cancels a held or retried activity on purpose.
    /// why: a cancel that lands while a short activity returns makes the time-skipping server refuse the workflow's answer (ACTIVITY_UNKNOWN) and never retry it, so the run hangs (issue 2877); a held activity never parks, so those tests opt out
    /// </param>
    /// <returns>The final run row.</returns>
    public async Task<WorkflowRun> Cancel(WorkflowRunId id, bool whenParked = true)
    {
        if (whenParked)
        {
            await Parked(id);
        }

        await Client.GetWorkflowHandle(id.Value).CancelAsync();
        try
        {
            return await Final(id, _cancelHangLimit);
        }
        catch (OperationCanceledException e) when (!Token.IsCancellationRequested)
        {
            throw new InvalidOperationException($"Run {id.Value} never reached a final status within {_cancelHangLimit} after the cancel (hang limit). {await HistoryOf(id)}", e);
        }
    }

    // why: see Cancel's whenParked; the run is parked when nothing runs after its last completed workflow task: the history ends there or at a timer the workflow started
    private async Task Parked(WorkflowRunId id)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(_patience);
        var last = new List<string>();
        try
        {
            while (true)
            {
                var history = await Client.GetWorkflowHandle(id.Value).FetchHistoryAsync(new WorkflowHistoryEventFetchOptions { Rpc = new RpcOptions { CancellationToken = limit.Token } });
                if (history.Events.LastOrDefault()?.EventType is Temporalio.Api.Enums.V1.EventType.WorkflowTaskCompleted or Temporalio.Api.Enums.V1.EventType.TimerStarted)
                {
                    return;
                }

                last = [.. history.Events.TakeLast(5).Select(static e => e.EventType.ToString())];
                await Task.Delay(TimeSpan.FromMilliseconds(25), limit.Token);
            }
        }
        catch (OperationCanceledException e) when (limit.IsCancellationRequested && !Token.IsCancellationRequested)
        {
            throw new InvalidOperationException($"Run {id.Value} never parked within {_patience} (history ends at: {string.Join(", ", last)}); pass whenParked: false to cancel a held activity on purpose.", e);
        }
    }

    // why: a hang after a cancel says nothing on its own (issue 2877); the event types, the last activity failures and the run row show where the run stopped
    private async Task<string> HistoryOf(WorkflowRunId id)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            var history = await Client.GetWorkflowHandle(id.Value).FetchHistoryAsync(new WorkflowHistoryEventFetchOptions { Rpc = new RpcOptions { CancellationToken = limit.Token } });
            var events = history.Events.Select(static e => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{e.EventId}:{e.EventType}")).ToList();
            var failures = history.Events
                .Where(static e => e.ActivityTaskFailedEventAttributes != null)
                .Select(static e => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{e.EventId}: {e.ActivityTaskFailedEventAttributes.Failure?.Message}"))
                .TakeLast(5);
            var row = await Runs.Get(id, limit.Token);
            var nodes = await NodeStatuses(id);
            var worker = _loop.IsFaulted ? $"faulted: {_loop.Exception?.GetBaseException().Message}" : _loop.Status.ToString();
            return $"Worker: {worker}. Worker log (last 15): {string.Join(" || ", _log.Last(15))}. Run row: {(row.IsSuccess ? row.Value.Status.ToString() : row.Error.Code)}. Nodes: {string.Join(", ", nodes.Select(static n => $"{n.Key}={n.Value}"))}. " +
                $"History ({events.Count}): {string.Join(' ', events.TakeLast(40))}. Activity failures: {string.Join(" | ", failures)}";
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return $"(history unavailable: {e.GetType().Name}: {e.Message})";
        }
    }

    /// <summary>
    /// Waits until the run's Temporal history shows the cancel request, under the hang limit (5 min) and not the check patience,
    /// so a loaded box cannot fail the wait and only a lost cancel does (issue 2678).
    /// </summary>
    public async Task CancelRequested(WorkflowRunId id)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(_cancelHangLimit);
        try
        {
            while (true)
            {
                var history = await Client.GetWorkflowHandle(id.Value).FetchHistoryAsync();
                if (history.Events.Any(static e => e.WorkflowExecutionCancelRequestedEventAttributes != null))
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
            }
        }
        catch (OperationCanceledException e) when (limit.IsCancellationRequested && !Token.IsCancellationRequested)
        {
            throw new InvalidOperationException($"The history of run {id.Value} never showed WorkflowExecutionCancelRequested within {_cancelHangLimit} (hang limit).", e);
        }
    }

    /// <summary>Waits until the run has a final status.</summary>
    public async Task<WorkflowRun> Final(WorkflowRunId id, TimeSpan? patience = null)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(patience ?? _patience);
        while (true)
        {
            var run = (await Runs.Get(id, limit.Token)).Value;
            if (run.Status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled)
            {
                return run;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
        }
    }

    /// <summary>
    /// Like <see cref="Rows"/>, but waits until the ledger holds <paramref name="expected"/>. The run's status is written
    /// before its closing ledger row ('workflow.run.finished' or 'workflow.run.cancelled'), so <see cref="Final"/> can
    /// return first. On timeout it returns the last rows so the caller's assert shows what was written.
    /// </summary>
    public async Task<IReadOnlyList<string>> RowsWith(string expected)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(_patience);
        var rows = await Rows();
        try
        {
            while (!rows.Contains(expected, StringComparer.Ordinal))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
                rows = await Rows();
            }
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested && !Token.IsCancellationRequested)
        {
            // timed out: the caller's assert reports the rows
        }

        return rows;
    }

    public async Task<IReadOnlyDictionary<string, NodeRunStatus>> NodeStatuses(WorkflowRunId id) =>
        (await Runs.NodeRuns(id, Token)).Value.ToDictionary(static n => n.NodeId.Value, static n => n.Status, StringComparer.Ordinal);

    /// <summary>Every row of one node, by item index (0 is the node's own row).</summary>
    public async Task<IReadOnlyDictionary<int, NodeRun>> ItemRuns(WorkflowRunId id, string node) =>
        (await Runs.NodeRuns(id, Token)).Value.Where(n => string.Equals(n.NodeId.Value, node, StringComparison.Ordinal)).ToDictionary(static n => n.ItemIndex);

    /// <summary>The payloads of every ledger event of one type, oldest first (#3151).</summary>
    public async Task<IReadOnlyList<JsonObject>> Payloads(string type) =>
        [.. (await _ledger.ReadAfter(0, 1000, Token)).Value.Where(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal)).Select(static e => JsonNode.Parse(e.Envelope.Payload)!.AsObject())];

    /// <summary>The ledger rows the run wrote, as '&lt;type&gt; &lt;idempotency key&gt;', oldest first.</summary>
    public async Task<IReadOnlyList<string>> Rows()
    {
        var read = (await _ledger.ReadAfter(0, 1000, Token)).Value;
        return [.. read.Where(static e => e.Envelope.Type.StartsWith("workflow.run.", StringComparison.Ordinal) || e.Envelope.Type.StartsWith("workflow.node.", StringComparison.Ordinal))
            .Select(static e => $"{e.Envelope.Type} {e.IdempotencyKey}")];
    }

    public async ValueTask DisposeAsync()
    {
        Blocks.Release();
        await _stop.CancelAsync();
        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
            // the worker stopped
        }

        _worker.Dispose();
        _stop.Dispose();
        // why: a task the worker held when it stopped keeps time skipping locked on its server (issue 2574); the server is the rig's own, so it dies with the rig
        await _server.DisposeAsync();
    }

    // why: a hung cancel left the worker's state unknown (issue 2877); the last lines the worker logged go in the hang message
    private sealed class RigLog : ILoggerFactory
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _lines = new();

        public IEnumerable<string> Last(int count) => _lines.ToArray().TakeLast(count);

        public ILogger CreateLogger(string categoryName) => new Writer(this, categoryName);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private void Add(string line)
        {
            _lines.Enqueue(line);
            while (_lines.Count > 200 && _lines.TryDequeue(out _))
            {
            }
        }

        private sealed class Writer(RigLog log, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                {
                    log.Add($"{logLevel} {category}: {formatter(state, exception)} {exception?.GetBaseException().Message}");
                }
            }
        }
    }
}
