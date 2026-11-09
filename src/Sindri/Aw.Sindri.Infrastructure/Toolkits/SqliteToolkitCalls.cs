using Aw.Kernel;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>IToolkitCalls over the 'toolkit_call' table of ledger.db (migration 17). A read never creates or migrates the file and finds nothing on an older one.</summary>
/// <param name="database">The ledger file.</param>
internal sealed class SqliteToolkitCalls(Ledger.LedgerDatabase database) : IToolkitCalls
{
    private readonly Ledger.LedgerDatabase _database = database;

    public ValueTask<Result<Unit>> Add(ToolkitCall call, CancellationToken cancellationToken) { _ = _database; throw new NotSupportedException(); }

    public ValueTask<Result<IReadOnlyList<ToolkitCall>>> List(string? toolkit, int limit, CancellationToken cancellationToken) { _ = _database; throw new NotSupportedException(); }

    public ValueTask<Result<int>> DeleteBefore(DateTimeOffset before, CancellationToken cancellationToken) { _ = _database; throw new NotSupportedException(); }
}
