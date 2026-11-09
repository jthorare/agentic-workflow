using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>'workflow_seen' in ledger.db (migration 13): the keys an 'only_new' node has passed on. A read never creates or migrates the file.</summary>
/// <param name="database">The ledger file.</param>
/// <param name="clock">The clock the first-seen times come from.</param>
internal sealed class SqliteSeenStore(LedgerDatabase database, TimeProvider clock) : IGraphSeenStore
{
    private const string IsSeen = "SELECT EXISTS (SELECT 1 FROM workflow_seen WHERE workflow_id = $workflow AND key = $key)";

    private const string MarkSeen = "INSERT OR IGNORE INTO workflow_seen (workflow_id, key, first_seen_at) VALUES ($workflow, $key, $at)";

    public ValueTask<Result<IReadOnlySet<string>>> Seen(WorkflowId workflow, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return RecipeSessions.Read(database, LedgerDatabase.WorkflowSeenVersion, new HashSet<string>(StringComparer.Ordinal), async ValueTask<Result<IReadOnlySet<string>>> (session, token) =>
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                var exists = await RecipeSessions.Exists(session, new SqlStatement(IsSeen, c => c.Bind("$workflow", workflow.Value).Bind("$key", key)), token).ConfigureAwait(false);
                if (exists.IsError)
                {
                    return exists.Error;
                }

                if (exists.Value)
                {
                    _ = found.Add(key);
                }
            }

            return Results.Success<IReadOnlySet<string>>(found);
        }, cancellationToken);
    }

    public ValueTask<Result<Unit>> Record(WorkflowId workflow, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        return RecipeSessions.Write(database, async ValueTask<Result<Unit>> (session, token) =>
        {
            var at = clock.GetUtcNow();
            foreach (var key in keys)
            {
                var marked = await RecipeSessions.Execute(session, new SqlStatement(MarkSeen, c => c.Bind("$workflow", workflow.Value).Bind("$key", key).Bind("$at", at), $"seen {key}"), token).ConfigureAwait(false);
                if (marked.IsError)
                {
                    return marked.Error;
                }
            }

            return Unit.Value;
        }, cancellationToken);
    }
}
