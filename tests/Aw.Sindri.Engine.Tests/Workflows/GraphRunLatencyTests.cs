using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Logging.Abstractions;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// A manual -> notify run on the real stack: a real 'temporal server start-dev', the SQLite run store, workflow store and ledger on a temp file, the system clock (#3685).
/// Every activity of a run is one round trip to that server, so this is the check that a run with no work in it is not slow. Heavy: it starts the dev server.
/// </summary>
[Lane(TestLane.Heavy)]
public sealed class GraphRunLatencyTests : IDisposable
{
    private const int Runs = 10;
    private static readonly TimeSpan _budget = TimeSpan.FromMilliseconds(1000);
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 180_000)]
    public async Task A_manual_to_notify_run_ends_in_under_a_second_at_p95_on_the_dev_server()
    {
        var ct = TestContext.Current.CancellationToken;
        var server = new DevServerTemporal(new TemporalSettings(_home.Path, PinnedTemporal.Path()), new TemporalDevServer(ChildEnvironment.Start));
        var endpoint = (await server.Ensure(ct)).Value;
        try
        {
            var spans = await Measure(endpoint, ct);

            var ordered = spans.Order().ToList();
            var p95 = ordered[(int)Math.Ceiling(0.95 * ordered.Count) - 1];
            Assert.True(p95 < _budget, $"p95 of startedAt -> endedAt was {p95.TotalMilliseconds:F0} ms over {ordered.Count} runs (budget {_budget.TotalMilliseconds:F0} ms): {string.Join(", ", spans.Select(s => s.TotalMilliseconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)))}");
        }
        finally
        {
            _ = await server.Stop(ct);
        }
    }

    private async Task<List<TimeSpan>> Measure(TemporalEndpoint endpoint, CancellationToken ct)
    {
        var clock = TimeProvider.System;
        using var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), clock);
        var probe = new FakeProcessProbe();
        var signal = new LedgerSignal(clock);
        var ledger = new SqliteLedger(database, signal);
        var held = new HeldLease();
        held.Hold((await new SqliteLease(database, probe, clock).Acquire(probe.Self(), ct)).Value);
        var runs = new SqliteWorkflowRunStore(database, clock);
        var workflows = new SqliteWorkflowStore(database, clock);
        var ids = new SequentialIdGenerator();
        var mirror = new GraphMirror(ledger, held, new EventFactory(EventScope.ControlPlane, clock, ids, SliceHarness.Json, new AsyncLocalCallerContextAccessor(ids)));
        var client = await TemporalClient.ConnectAsync(new TemporalClientConnectOptions($"127.0.0.1:{endpoint.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)}") { Namespace = TemporalSettings.DefaultNamespace });
        var queue = "graph-latency-test";
        var starter = new GraphRunStarter(runs, new TemporalGraphRunLauncher(_ => ValueTask.FromResult(Results.Success<ITemporalClient>(client)), queue, 0), mirror, clock, NullLogger<GraphRunStarter>.Instance);
        var registry = new GraphBlockRegistry([new ManualBlock(), new NotifyBlock(new QuietNotify())]);
        var options = new TemporalWorkerOptions(queue)
            .AddWorkflow<GraphWorkflow>()
            .AddWorkflow<GraphIterationWorkflow>()
            .AddAllActivities(new GraphRunActivities(runs, workflows, starter, mirror, new GraphRunItemRepair(new SpyRunItemRepair(), NullLogger<GraphRunItemRepair>.Instance)))
            .AddAllActivities(new GraphBlockActivities(registry, runs, workflows));
        using var worker = new TemporalWorker(client, options);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var loop = worker.ExecuteAsync(stop.Token);
        try
        {
            var id = WorkflowId.From("WF-00000000000000000000000000000001");
            var slug = WorkflowSlug.From("latency-test");
            var spec = GraphRig.Spec("unlimited",
                [GraphRig.Node("start", "manual"), GraphRig.Node("tell", "notify", config: new JsonObject { ["title"] = "done" })],
                [GraphRig.Edge("start", "tell")]);
            _ = (await workflows.Create(id, slug, spec, "owner", ct)).Value;

            var spans = new List<TimeSpan>();
            for (var i = 0; i <= Runs; i++)   // run 0 is the warm-up: it pays the cold connect and the first workflow task
            {
                var started = (await starter.Start(new GraphStartRequest(id, slug, Revision: 1, Occurrence: i, """{"kind":"manual"}""", GraphConcurrency.Unlimited), ct)).Value;
                var run = await Final(runs, started.RunId, ct);
                if (i > 0)
                {
                    spans.Add(run.EndedAt!.Value - run.StartedAt);
                }
            }

            return spans;
        }
        finally
        {
            await stop.CancelAsync();
            try
            {
                await loop;
            }
            catch (OperationCanceledException)
            {
                // the worker stopped
            }
        }
    }

    private static async Task<WorkflowRun> Final(SqliteWorkflowRunStore runs, WorkflowRunId id, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            var run = (await runs.Get(id, limit.Token)).Value;
            if (run.Status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled)
            {
                return run;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), limit.Token);
        }
    }

    // why: the notify block's own write is a ledger append the run does not wait on beyond one call; the timing is the walk's, so the port only answers
    private sealed class QuietNotify : INotifyPort
    {
        public ValueTask<Result<Unit>> Post(WorkflowRunId runId, NodeId node, string title, string? body, CancellationToken cancellationToken) => ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
