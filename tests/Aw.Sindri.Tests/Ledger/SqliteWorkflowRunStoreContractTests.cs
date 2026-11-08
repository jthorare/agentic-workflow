using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>'workflow_run' and 'workflow_node_run' in ledger.db against the run store contract, on a fresh file.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteWorkflowRunStoreContractTests : WorkflowRunStoreContract, IDisposable
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

    protected override IWorkflowRunStore Create(TimeProvider clock)
    {
        var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), clock);
        _databases.Add(database);
        return new SqliteWorkflowRunStore(database, clock);
    }
}
