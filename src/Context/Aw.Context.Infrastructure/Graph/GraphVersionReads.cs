using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The version list's read. A store not yet upgraded (reads never migrate) answers with no ruleset on any version before 0012, no dropped counts before 0013 and no structure reason before 0016.</summary>
internal static class GraphVersionReads
{
    public static async ValueTask<Result<IReadOnlyList<MapVersion>>> List(SqliteReadSession session, CancellationToken cancellationToken)
    {
        var ruleset = await SqliteQueries.One(session, new SqlStatement(GraphSql.HasRulesetColumn), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        var dropped = ruleset.IsError ? ruleset : await SqliteQueries.One(session, new SqlStatement(GraphSql.HasDroppedColumn), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        var checks = dropped.IsError ? dropped : await SqliteQueries.One(session, new SqlStatement(GraphSql.HasChecksColumn), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        if (checks.IsError)
        {
            return checks.Error;
        }

        var select = checks.Value > 0 ? GraphSql.SelectVersions : dropped.Value > 0 ? GraphSql.SelectVersionsBefore0017 : ruleset.Value > 0 ? GraphSql.SelectVersionsBefore0013 : GraphSql.SelectVersionsBefore0012;
        var versions = await SqliteQueries.List(session, new SqlStatement(select), GraphRows.Version, cancellationToken).ConfigureAwait(false);
        return versions.IsError ? versions : await WithPinHashes(session, versions.Value, cancellationToken).ConfigureAwait(false);
    }

    // The per-pin hashes live in their own column (0028): a store before it, or a version without them, answers null, never a guess.
    private static async ValueTask<Result<IReadOnlyList<MapVersion>>> WithPinHashes(SqliteReadSession session, IReadOnlyList<MapVersion> versions, CancellationToken cancellationToken)
    {
        var has = await SqliteQueries.One(session, new SqlStatement(GraphSql.HasPinHashesColumn), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        if (has.IsError || has.Value == 0)
        {
            return has.IsError ? has.Error : Results.Success(versions);
        }

        var stored = await SqliteQueries.List(session, new SqlStatement(GraphSql.SelectPinHashes), GraphRows.PinHashes, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var byVersion = stored.Value.ToDictionary(static s => s.Version, static s => PinHashesText.Read(s.Hashes));
        return Results.Success<IReadOnlyList<MapVersion>>([.. versions.Select(v => byVersion.TryGetValue(v.Number, out var hashes) ? v with { PinHashes = hashes } : v)]);
    }
}
