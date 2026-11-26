using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

// The filtered runs read (#4334): statuses, the start-time window and the 'before' cursor are part of the SQL, so the page cap never hides a match.
internal sealed partial class SqliteWorkflowRunStore
{
    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> Find(WorkflowId workflow, WorkflowRunQuery query, int offset, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // why: one bound text ',failed,cancelled,' instead of a variable IN list, so the statement text never changes
        var statuses = query.Statuses.Count == 0 ? null : "," + string.Join(',', query.Statuses.Select(static s => RunStatusText.Of(s))) + ",";
        return RecipeSessions.Read(database, LedgerDatabase.WorkflowRunsVersion, [],
            (session, token) => RecipeSessions.List(session, new SqlStatement(WorkflowRunSql.Find, c => c
                .Bind("$workflow", workflow.Value).Bind("$statuses", statuses)
                .Bind("$since", Stamp(query.Since)).Bind("$until", Stamp(query.Until))
                .Bind("$beforeAt", Stamp(query.Before?.StartedAt)).Bind("$beforeOccurrence", query.Before?.Id.Occurrence)
                .Bind("$limit", Math.Max(limit, 1)).Bind("$offset", Math.Max(offset, 0))), WorkflowRunMapper.Instance, token), cancellationToken);
    }

    // why: the same text SqliteBind writes for started_at (UTC, round-trip 'O'), so the comparisons in the SQL are text order on one fixed width
    private static string? Stamp(DateTimeOffset? at) => at?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
