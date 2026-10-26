using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The version list's read. A store not yet upgraded to 0012 (reads never migrate) answers with no ruleset on any version.</summary>
internal static class GraphVersionReads
{
    public static async ValueTask<Result<IReadOnlyList<MapVersion>>> List(SqliteReadSession session, CancellationToken cancellationToken)
    {
        var upgraded = await SqliteQueries.One(session, new SqlStatement(GraphSql.HasRulesetColumn), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        return upgraded.IsError
            ? upgraded.Error
            : await SqliteQueries.List(session, new SqlStatement(upgraded.Value > 0 ? GraphSql.SelectVersions : GraphSql.SelectVersionsBefore0012), GraphRows.Version, cancellationToken).ConfigureAwait(false);
    }
}
