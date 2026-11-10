using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>IToolkitCalls over the 'toolkit_call' table of ledger.db (migration 17). A read never creates or migrates the file and finds nothing on an older one.</summary>
/// <param name="database">The ledger file.</param>
internal sealed class SqliteToolkitCalls(LedgerDatabase database) : IToolkitCalls
{
    private readonly LedgerDatabase _database = database;

    public ValueTask<Result<Unit>> Add(ToolkitCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        return RecipeSessions.Write(_database, async ValueTask<Result<Unit>> (session, token) =>
        {
            var statement = new SqlStatement(ToolkitCallSql.Insert, c => c
                .Bind("$toolkit", call.Toolkit).Bind("$tool", call.Tool).Bind("$caller", call.Caller).Bind("$input", call.Input).Bind("$output", call.Output)
                .Bind("$truncated", call.Truncated ? 1 : 0).Bind("$error", call.Error).Bind("$durationMs", call.DurationMs).Bind("$at", call.At), $"call of {call.Toolkit}");
            var inserted = await RecipeSessions.Execute(session, statement, token).ConfigureAwait(false);
            return inserted.IsError ? inserted.Error : Unit.Value;
        }, cancellationToken);
    }

    public ValueTask<Result<IReadOnlyList<ToolkitCall>>> List(string? toolkit, int limit, CancellationToken cancellationToken)
        => RecipeSessions.Read(_database, LedgerDatabase.ToolkitsVersion, [],
            (session, token) => RecipeSessions.List(session, new SqlStatement(ToolkitCallSql.Select, c => c.Bind("$toolkit", toolkit).Bind("$limit", limit), "toolkit calls"), ToolkitCallMapper.Instance, token), cancellationToken);

    public ValueTask<Result<int>> DeleteBefore(DateTimeOffset before, CancellationToken cancellationToken)
        => RecipeSessions.Write(_database, (session, token) =>
            RecipeSessions.Execute(session, new SqlStatement(ToolkitCallSql.DeleteBefore, c => c.Bind("$before", before), "old toolkit calls"), token), cancellationToken);
}
