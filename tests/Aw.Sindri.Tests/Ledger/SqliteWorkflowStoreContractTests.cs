using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
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
}
