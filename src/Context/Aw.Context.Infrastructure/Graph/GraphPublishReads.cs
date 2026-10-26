using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The publish state's reads (retrieval spec §8.7a). A store not yet upgraded to 0010 (reads never migrate) holds nothing and has no heads.</summary>
internal static class GraphPublishReads
{
    // The versions, the hold and the heads in one read session.
    public static async ValueTask<Result<MapPublishState>> State(SqliteReadSession session, CancellationToken cancellationToken)
    {
        var versions = await GraphVersionReads.List(session, cancellationToken).ConfigureAwait(false);
        var upgraded = await SqliteQueries.One(session, new SqlStatement(GraphSql.HasPublishTables), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        if (versions.IsError || upgraded.IsError)
        {
            return versions.IsError ? versions.Error : upgraded.Error;
        }

        var draft = versions.Value.Where(v => v.Status == MapVersionStatus.Draft).Select(v => (int?)v.Number).FirstOrDefault();
        var current = versions.Value.Where(v => v.Status == MapVersionStatus.Current).Select(v => (int?)v.Number).FirstOrDefault();
        var state = upgraded.Value < 3 ? new MapPublishState(Hold: null, draft, current, CurrentHeads: []) : await WithHold(session, draft, current, cancellationToken).ConfigureAwait(false);
        return state.IsError ? state : state.Value with { CurrentRuleset = versions.Value.FirstOrDefault(static v => v.Status == MapVersionStatus.Current)?.Ruleset };
    }

    public static async ValueTask<Result<IReadOnlyList<MapHead>>> Heads(SqliteReadSession session, int version, CancellationToken cancellationToken)
    {
        var upgraded = await SqliteQueries.One(session, new SqlStatement(GraphSql.HasPublishTables), GraphRows.Id, cancellationToken).ConfigureAwait(false);
        return upgraded.IsError ? upgraded.Error
            : upgraded.Value < 3 ? Results.Success<IReadOnlyList<MapHead>>([])
            : await SqliteQueries.List(session, new SqlStatement(GraphSql.SelectHeads, c => c.Bind("$v", (long)version)), GraphRows.Head, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Result<MapPublishState>> WithHold(SqliteReadSession session, int? draft, int? current, CancellationToken cancellationToken)
    {
        var held = await SqliteQueries.List(session, new SqlStatement(GraphSql.SelectHold), GraphRows.Hold, cancellationToken).ConfigureAwait(false);
        var currentHeads = current is { } c ? await Heads(session, c, cancellationToken).ConfigureAwait(false) : Results.Success<IReadOnlyList<MapHead>>([]);
        if (held.IsError || currentHeads.IsError)
        {
            return held.IsError ? held.Error : currentHeads.Error;
        }

        if (held.Value.Count == 0)
        {
            return new MapPublishState(Hold: null, draft, current, currentHeads.Value);
        }

        var (version, since) = held.Value[0];
        var heads = await Heads(session, version, cancellationToken).ConfigureAwait(false);
        return heads.IsError ? heads.Error : new MapPublishState(new MapHold(version, GraphText.Time(since) ?? default, heads.Value), draft, current, currentHeads.Value);
    }
}
