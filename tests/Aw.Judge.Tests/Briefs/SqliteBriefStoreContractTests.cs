using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Sqlite;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Briefs;

[Lane(TestLane.Integration)]
public sealed class SqliteBriefStoreContractTests : BriefStoreContract, IDisposable
{
    private readonly TempStateDirectory _state = new();

    protected override ValueTask<IBriefStore> Create(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IBriefStore>(new SqliteBriefStore(_state.Database()));

    protected override ValueTask<IBriefStore> CreateUnavailable(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IBriefStore>(new SqliteBriefStore(_state.UnopenableDatabase()));

    public void Dispose() => _state.Dispose();
}
