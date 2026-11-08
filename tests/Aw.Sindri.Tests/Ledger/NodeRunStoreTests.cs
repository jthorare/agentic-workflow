using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>The 64 KB cap on a node run's stored input and output (issue 2258, PR 6), on both stores.</summary>
[Lane(TestLane.Integration)]
public sealed class NodeRunStoreTests : IDisposable
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
    public async Task Node_output_over_the_cap_is_truncated_and_flagged()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), clock);
        _databases.Add(database);
        IWorkflowRunStore[] stores = [new SqliteWorkflowRunStore(database, clock), new InMemoryWorkflowRunStore(clock)];

        foreach (var store in stores)
        {
            var run = new WorkflowRun(WorkflowRunId.From("big-output@1:1"), WorkflowId.From("WF-00000000000000000000000000000002"), 1, "{}",
                WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, KnownInstants.Epoch, EndedAt: null);
            _ = (await store.Insert(run, TestContext.Current.CancellationToken)).Value;
            var big = new string('x', NodeRunLimits.PayloadBytes + 10);
            var small = new NodeRun(run.Id, NodeId.From("fetch"), 0, NodeRunStatus.Succeeded, Input: "{}", Output: "{\"ok\":true}", Truncated: false, Error: null, Usage: null);

            var kept = (await store.RecordNodeRun(small, TestContext.Current.CancellationToken)).Value;
            var cut = (await store.RecordNodeRun(small with { ItemIndex = 1, Output = big }, TestContext.Current.CancellationToken)).Value;

            Assert.False(kept.Truncated);
            Assert.Equal("{\"ok\":true}", kept.Output);
            Assert.True(cut.Truncated);
            Assert.Equal(NodeRunLimits.PayloadBytes, Encoding.UTF8.GetByteCount(cut.Output!));
            var stored = (await store.NodeRuns(run.Id, TestContext.Current.CancellationToken)).Value;
            Assert.Equal([false, true], stored.Select(static r => r.Truncated));
            Assert.Equal(NodeRunLimits.PayloadBytes, Encoding.UTF8.GetByteCount(stored[1].Output!));
        }
    }
}
