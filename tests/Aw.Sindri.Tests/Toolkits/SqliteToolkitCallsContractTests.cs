using Aw.Kernel.Testing;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Toolkits;
using Aw.Sindri.Testing.Toolkits;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Toolkits;

/// <summary>The 'toolkit_call' table of ledger.db against the call store contract, on a fresh file.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteToolkitCallsContractTests : ToolkitCallsContract, IDisposable
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

    protected override IToolkitCalls Create()
    {
        var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), new FakeTimeProvider(KnownInstants.Epoch));
        _databases.Add(database);
        return new SqliteToolkitCalls(database);
    }
}
