using Aw.Judge.Core.Ports;
using Aw.Judge.Host;
using Aw.Judge.Infrastructure.Sqlite;
using Aw.Judge.Testing;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Decisions;

[Lane(TestLane.Integration)]
public sealed class SqliteDecisionLogContractTests : DecisionLogContract, IDisposable
{
    private readonly TempStateDirectory _state = new();

    protected override ValueTask<IDecisionLog> Create(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IDecisionLog>(_state.DecisionLog());

    protected override ValueTask<IDecisionLog> CreateUnavailable(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IDecisionLog>(new SqliteDecisionLog(_state.UnopenableDatabase(), AwJson.CreateOptions(JudgeJsonContext.Default)));

    public void Dispose() => _state.Dispose();
}
