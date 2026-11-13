using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>P3e (#3196, spec §6.2): raw is stored on the node run by both stores, as given (the activity scrubs and cuts it first).</summary>
[Lane(TestLane.Integration)]
public sealed class NodeRunRawStoreTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly List<LedgerDatabase> _databases = [];

    public void Dispose()
    {
        foreach (var database in _databases)
        {
            database.Dispose();
        }

        _home.Dispose();
    }

    [Fact]
    public async Task Raw_text_is_stored_and_read_back_on_the_node_run()
    {
        await ForEachStore(async (store, run) =>
        {
            _ = (await store.RecordNodeRun(Failed(run, "Usage limit reached."), TestContext.Current.CancellationToken)).Value;

            var stored = (await store.NodeRuns(run.Id, TestContext.Current.CancellationToken)).Value.Single();
            Assert.Equal("Usage limit reached.", stored.Raw);
        });
    }

    [Fact]
    public async Task A_node_run_without_raw_reads_back_null()
    {
        await ForEachStore(async (store, run) =>
        {
            _ = (await store.RecordNodeRun(Failed(run, raw: null), TestContext.Current.CancellationToken)).Value;

            Assert.Null((await store.NodeRuns(run.Id, TestContext.Current.CancellationToken)).Value.Single().Raw);
        });
    }

    private static NodeRun Failed(WorkflowRun run, string? raw) =>
        new(run.Id, NodeId.From("ask"), 0, NodeRunStatus.Failed, Input: "{}", Output: null, Truncated: false, Error: "SND-GRAPH-103: x", Usage: null, Raw: raw);

    private async Task ForEachStore(Func<IWorkflowRunStore, WorkflowRun, Task> check)
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), clock);
        _databases.Add(database);
        IWorkflowRunStore[] stores = [new SqliteWorkflowRunStore(database, clock), new InMemoryWorkflowRunStore(clock)];
        foreach (var store in stores)
        {
            var run = new WorkflowRun(WorkflowRunId.From("raw-run@1:1"), WorkflowId.From("WF-00000000000000000000000000000003"), 1, "{}",
                WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, KnownInstants.Epoch, EndedAt: null);
            _ = (await store.Insert(run, TestContext.Current.CancellationToken)).Value;
            await check(store, run);
        }
    }
}
