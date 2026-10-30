using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Graph;

internal sealed partial class GraphWrites
{
    // What the version records of where its repos stood: the build's own heads, else a replaced draft's, else (an import) the one commit it names.
    private static IEnumerable<SqlStatement> HeadStatements(MapImport map, long version, bool draft, int? replacing)
    {
        if (map.Heads is { } heads)
        {
            return heads.Select(h => new SqlStatement(GraphSql.InsertHead, c => c.Bind("$v", version).Bind("$repo", h.Repo.Value).Bind("$commit", h.Commit).Bind("$onDefault", h.OnDefault ? 1L : 0L).Bind("$indexCommit", h.IndexCommit)));
        }

        if (replacing is { } source)
        {
            return [new SqlStatement(GraphSql.CopyHeads, c => c.Bind("$to", version).Bind("$from", (long)source))];
        }

        return !draft && map.Commit is { } commit && MapHead.IsCommit(commit)
            ? [new SqlStatement(GraphSql.InsertHead, c => c.Bind("$v", version).Bind("$repo", map.Repo.Value).Bind("$commit", commit).Bind("$onDefault", 1L).Bind("$indexCommit", (string?)null))]
            : [];
    }

    /// <summary>The open draft, or a copy of the current version made in one transaction when there is none.</summary>
    public async ValueTask<Result<MapVersion>> OpenDraft(DateTimeOffset created, CancellationToken cancellationToken)
    {
        var begun = await sessions.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var versions = await SqliteQueries.List(begun.Value, new SqlStatement(GraphSql.SelectVersions), GraphRows.Version, cancellationToken).ConfigureAwait(false);
            if (versions.IsError)
            {
                return ContextDatabase.Translate(versions.Error);
            }

            if (versions.Value.FirstOrDefault(v => v.Status == MapVersionStatus.Draft) is { } open)
            {
                return open;
            }

            if (versions.Value.FirstOrDefault(v => v.Status == MapVersionStatus.Current) is not { } current)
            {
                return ContextErrors.NoMap.With("there is no current map to open a draft from (fix: sindri context map import <file>)");
            }

            var copy = await SqliteQueries.One(begun.Value, new SqlStatement(GraphSql.CopyVersion, c => c.Bind("$created", created).Bind("$from", (long)current.Number)), GraphRows.Id, cancellationToken).ConfigureAwait(false);
            if (copy.IsError)
            {
                return ContextDatabase.Translate(copy.Error);
            }

            var copied = await CopyGraph(begun.Value, current.Number, copy.Value, cancellationToken).ConfigureAwait(false);
            if (copied.IsError)
            {
                return ContextDatabase.Translate(copied.Error);
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : current with { Number = (int)copy.Value, Created = created, Status = MapVersionStatus.Draft };
        }
    }

    private static async ValueTask<Result<Unit>> CopyGraph(ISqliteSession session, int from, long to, CancellationToken cancellationToken)
    {
        foreach (var sql in GraphSql.CopyGraph)
        {
            var copied = await SqliteQueries.Execute(session, new SqlStatement(sql, c => c.Bind("$to", to).Bind("$from", (long)from)), cancellationToken).ConfigureAwait(false);
            if (copied.IsError)
            {
                return copied.Error;
            }
        }

        return Unit.Value;
    }
}
