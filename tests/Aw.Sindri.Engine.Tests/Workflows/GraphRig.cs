using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
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
    private const string TaskQueue = "graph-test";
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(30);
    private readonly CancellationTokenSource _stop = new();
    private readonly TemporalWorker _worker;
    private readonly Task _loop;
    private readonly GraphRunStarter _starter;

    private GraphRig(WorkflowEnvironment environment)
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        Runs = new InMemoryWorkflowRunStore(clock);
        Workflows = new InMemoryWorkflowStore(clock);
        Blocks = new GraphTestBlocks();
        Client = environment.Client;
        var registry = new GraphBlockRegistry([new ManualBlock(), new ConditionBlock(), .. Blocks.All()]);
        var starter = new GraphRunStarter(Runs, new TemporalGraphRunLauncher(Client, TaskQueue), clock, NullLogger<GraphRunStarter>.Instance);
        _starter = starter;
        var options = new TemporalWorkerOptions(TaskQueue)
            .AddWorkflow<GraphWorkflow>()
            .AddAllActivities(new GraphRunActivities(Runs, Workflows, starter))
            .AddAllActivities(new GraphBlockActivities(registry, Runs, Workflows));
        _worker = new TemporalWorker(Client, options);
        _loop = _worker.ExecuteAsync(_stop.Token);
    }

    public InMemoryWorkflowRunStore Runs { get; }

    public InMemoryWorkflowStore Workflows { get; }

    public GraphTestBlocks Blocks { get; }

    public ITemporalClient Client { get; }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static GraphRig Start(WorkflowEnvironment environment) => new(environment);

    public static GraphNode Node(string id, string type, string? onFailure = null, JsonObject? config = null) =>
        new(NodeId.From(id), type, Title: null, config ?? [], onFailure);

    public static GraphEdge Edge(string from, string to, string? output = null) => new(NodeId.From(from), output, NodeId.From(to));

    public static GraphSpec Spec(string concurrency, IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges) =>
        new(Version: 1, "Walk test", Description: null, concurrency, Resources: null, nodes, edges);

    /// <summary>Saves the spec as revision 1 of a new workflow.</summary>
    public async Task<(WorkflowId Id, WorkflowSlug Slug)> Define(GraphSpec spec)
    {
        var id = WorkflowId.From("WF-00000000000000000000000000000001");
        var slug = WorkflowSlug.From("walk-test");
        _ = (await Workflows.Create(id, slug, spec, "owner", Token)).Value;
        return (id, slug);
    }

    /// <summary>Asks the starter for a run of revision 1, as a trigger does.</summary>
    public async Task<GraphStart> Trigger((WorkflowId Id, WorkflowSlug Slug) workflow, int occurrence, GraphConcurrency mode, string trigger = """{"kind":"manual"}""") =>
        (await _starter.Start(new GraphStartRequest(workflow.Id, workflow.Slug, Revision: 1, occurrence, trigger, mode), Token)).Value;

    /// <summary>Waits until the run has a final status.</summary>
    public async Task<WorkflowRun> Final(WorkflowRunId id)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(_patience);
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

    public async Task<IReadOnlyDictionary<string, NodeRunStatus>> NodeStatuses(WorkflowRunId id) =>
        (await Runs.NodeRuns(id, Token)).Value.ToDictionary(static n => n.NodeId.Value, static n => n.Status, StringComparer.Ordinal);

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
