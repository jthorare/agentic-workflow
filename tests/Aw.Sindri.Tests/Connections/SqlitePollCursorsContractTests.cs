using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Connections.Polling;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The 'poll_cursor' table of ledger.db against the cursor contract, on a fresh file.</summary>
[Lane(TestLane.Integration)]
public sealed class SqlitePollCursorsContractTests : PollCursorsContract, IDisposable
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

    protected override IPollCursors Create()
    {
        var database = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), new FakeTimeProvider(KnownInstants.Epoch));
        _databases.Add(database);
        return new SqlitePollCursors(database);
    }
}
