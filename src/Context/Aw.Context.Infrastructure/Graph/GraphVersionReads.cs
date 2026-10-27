using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The version list's read. A store not yet upgraded to 0012 (reads never migrate) answers with no ruleset on any version, one not yet upgraded to 0013 with no dropped counts.</summary>
internal static class GraphVersionReads
{
    public static async ValueTask<Result<IReadOnlyList<MapVersion>>> List(SqliteReadSession session, CancellationToken cancellationToken)
    {
        var ruleset = await SqliteQueries.One(session, new SqlStatement(GraphSql.HasRulesetColumn), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        var dropped = ruleset.IsError ? ruleset : await SqliteQueries.One(session, new SqlStatement(GraphSql.HasDroppedColumn), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        return dropped.IsError
            ? dropped.Error
            : await SqliteQueries.List(session, new SqlStatement(dropped.Value > 0 ? GraphSql.SelectVersions : ruleset.Value > 0 ? GraphSql.SelectVersionsBefore0013 : GraphSql.SelectVersionsBefore0012), GraphRows.Version, cancellationToken).ConfigureAwait(false);
    }
}
