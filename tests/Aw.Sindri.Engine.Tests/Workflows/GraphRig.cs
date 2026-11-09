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
internal sealed class GraphRig : IAsyncDisposable
{
    private static int _count;
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(30);
    private readonly string _taskQueue;
    private readonly CancellationTokenSource _stop = new();
    private readonly TemporalWorker _worker;
    private readonly Task _loop;
    private readonly GraphRunStarter _starter;
    private readonly InMemorySindriStore _ledger;

    private GraphRig(WorkflowEnvironment environment, ForEachLimits? forEachLimits)
    {
        // why: the test server is shared by the class, and a run id or task queue can be used once on it
        var number = Interlocked.Increment(ref _count).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Slug = $"walk-test-{number}";
        _taskQueue = $"graph-test-{number}";
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        Runs = new InMemoryWorkflowRunStore(clock);
        Workflows = new InMemoryWorkflowStore(clock);
        Blocks = new GraphTestBlocks();
        Client = environment.Client;
        var registry = new GraphBlockRegistry([new ManualBlock(), new ConditionBlock(), new ForEachBlock(forEachLimits is { } limits ? new FixedForEachLimits(limits) : new DefaultForEachLimits()), new CollectBlock(), .. Blocks.All()]);
        var probe = new FakeProcessProbe();
        _ledger = new InMemorySindriStore(clock, probe);
        var held = new HeldLease();
        held.Hold(_ledger.Acquire(probe.Self(), Token).AsTask().GetAwaiter().GetResult().Value);
        var ids = new SequentialIdGenerator();
        var mirror = new GraphMirror(_ledger, held, new EventFactory(EventScope.ControlPlane, clock, ids, SliceHarness.Json, new AsyncLocalCallerContextAccessor(ids)));
        var launcher = new TemporalGraphRunLauncher(_ => ValueTask.FromResult(Results.Success(Client)), _taskQueue);
        var starter = new GraphRunStarter(Runs, launcher, mirror, clock, NullLogger<GraphRunStarter>.Instance);
        _starter = starter;
        var options = new TemporalWorkerOptions(_taskQueue)
            .AddWorkflow<GraphWorkflow>()
            .AddWorkflow<GraphIterationWorkflow>()
            .AddAllActivities(new GraphRunActivities(Runs, Workflows, starter, mirror))
            .AddAllActivities(new GraphBlockActivities(registry, Runs, Workflows));
        _worker = new TemporalWorker(Client, options);
        _loop = _worker.ExecuteAsync(_stop.Token);
    }

    public InMemoryWorkflowRunStore Runs { get; }

    public InMemoryWorkflowStore Workflows { get; }

    public string Slug { get; }

    public GraphTestBlocks Blocks { get; }

    public ITemporalClient Client { get; }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static GraphRig Start(WorkflowEnvironment environment) => new(environment, forEachLimits: null);

    /// <summary>A rig whose for_each blocks read <paramref name="forEachLimits"/> as the profile's limits.</summary>
    /// <param name="environment">The shared Temporal test server.</param>
    /// <param name="forEachLimits">The profile's item cap and batch size.</param>
    /// <returns>The started rig.</returns>
    public static GraphRig Start(WorkflowEnvironment environment, ForEachLimits forEachLimits) => new(environment, forEachLimits);

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

    /// <summary>Asks the starter for a run of revision 1, as a trigger does.</summary>
    public async Task<GraphStart> Trigger((WorkflowId Id, WorkflowSlug Slug) workflow, int occurrence, GraphConcurrency mode, string trigger = """{"kind":"manual"}""") =>
        (await _starter.Start(new GraphStartRequest(workflow.Id, workflow.Slug, Revision: 1, occurrence, trigger, mode), Token)).Value;

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
    }
}
