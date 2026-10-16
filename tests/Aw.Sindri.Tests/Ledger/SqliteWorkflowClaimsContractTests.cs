using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>'workflow_claims' in ledger.db (engine spec §5.9; engine P6 unit (d)) against the claims contract, on a fresh file.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteWorkflowClaimsContractTests : WorkflowClaimsContract, IDisposable
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

    protected override IWorkflowClaims Create()
    {
        var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), new FakeTimeProvider(KnownInstants.Epoch));
        _databases.Add(database);
        return new SqliteWorkflowClaims(database, new FakeTimeProvider(KnownInstants.Epoch));
    }

    [Fact]
    public async Task A_ledger_older_than_the_claims_table_has_no_claim_and_isnt_migrated_by_a_read()
    {
        var claims = Create();

        var found = await claims.Find(Core.Workflow.Engine.ItemKey.From("T-3"), TestContext.Current.CancellationToken);

        Assert.Null(found.Value);
        Assert.False(File.Exists(Path.Combine(_home.Path, "ledger.db")));
    }
}
