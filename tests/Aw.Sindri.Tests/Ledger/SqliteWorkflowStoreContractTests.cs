using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>'workflow' and 'workflow_revision' in ledger.db against the store contract, on a fresh file.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteWorkflowStoreContractTests : WorkflowStoreContract, IDisposable
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

    protected override IWorkflowStore Create()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), clock);
        _databases.Add(database);
        return new SqliteWorkflowStore(database, clock);
    }

    /// <summary>Two saves on the same base revision race: one wins and the other gets the stale-revision code, never a busy or ledger error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Two_concurrent_saves_on_one_base_revision_give_one_winner_and_a_stale_conflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = Create();
        var id = WorkflowId.From("WF-00000000000000000000000000000001");
        _ = (await store.Create(id, WorkflowSlug.From("race"), Spec("one"), "owner", ct)).Value;

        var results = await Task.WhenAll(
            Task.Run(async () => await store.Save(id, 1, Spec("left"), "session:a", ct), ct),
            Task.Run(async () => await store.Save(id, 1, Spec("right"), "session:b", ct), ct));

        var loser = Expect.Only(results.Where(static r => r.IsError).ToList());
        Expect.Equal(SindriErrors.GraphStaleRevision.Code, loser.Error.Code);
        Expect.Equal(2, (await store.Get(id, ct)).Value.LatestRevision);
    }

    private static GraphSpec Spec(string name) =>
        new(Version: 1, Name: name, Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("daily"), "schedule", Title: null, new JsonObject { ["cron"] = "0 7 * * 1-5" })], Edges: []);
}
