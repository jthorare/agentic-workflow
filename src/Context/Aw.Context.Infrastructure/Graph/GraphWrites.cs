using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The graph store's writes, each statement list in one write transaction.</summary>
/// <param name="sessions">The migrated store's session factory.</param>
/// <param name="batchSize">Statements per transaction while a version is written.</param>
internal sealed partial class GraphWrites(SqliteUnitOfWorkFactory sessions, int batchSize)
{
    /// <summary>
    /// Writes <paramref name="map"/> as a 'building' version in batches, then retires the current one and makes it current
    /// (with the file's overrides) in one transaction. Leftover 'building' versions (an earlier crash) are removed first;
    /// a failed or cancelled save removes its own.
    /// </summary>
    public async ValueTask<Result<MapVersion>> Save(MapImport map, DateTimeOffset created, CancellationToken cancellationToken, bool draft = false, int? replacing = null)
    {
        if (map.Heads is { } heads && MapHead.Check(heads) is { } refused)
        {
            return refused;
        }

        // A replacement sweeps nothing: a 'building' version is another build's, still being written.
        var swept = replacing is null ? await Run([new SqlStatement(GraphSql.DeleteBuilding)], returnsId: false, cancellationToken).ConfigureAwait(false) : Results.Success(0L);
        var begun = swept.IsError ? swept : await Run([new SqlStatement(GraphSql.InsertVersion, c => c.Bind("$repo", map.Repo.Value).Bind("$commit", map.Commit)
            .Bind("$keyVersion", EntryKey.Version).Bind("$created", created).Bind("$ruleset", map.Ruleset))], returnsId: true, cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        var version = begun.Value;
        try
        {
            var written = await Write(map, version, draft, replacing, cancellationToken).ConfigureAwait(false);
            return written.IsError
                ? await Abandon(version, written.Error).ConfigureAwait(false)
                : new MapVersion((int)version, map.Repo, map.Commit, EntryKey.Version, created, draft ? MapVersionStatus.Draft : MapVersionStatus.Current) { Ruleset = map.Ruleset };
        }
        catch (Exception)
        {
            _ = await Abandon(version, error: null).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Deletes retained versions (never a current or building one); their graphs go with them (ON DELETE CASCADE).</summary>
    public async ValueTask<Result<int>> Delete(IReadOnlyList<int> versions, CancellationToken cancellationToken)
    {
        var deleted = await Run([.. versions.Select(v => new SqlStatement(GraphSql.DeleteRetained, c => c.Bind("$v", (long)v)))], returnsId: false, cancellationToken).ConfigureAwait(false);
        return deleted.IsError ? deleted.Error : (int)deleted.Value;
    }

    private async ValueTask<Result<long>> Write(MapImport map, long version, bool draft, int? replacing, CancellationToken cancellationToken)
    {
        foreach (var batch in GraphStatements.Graph(version, map.Graph).Chunk(batchSize))
        {
            var written = await Run(batch, returnsId: false, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written;
            }
        }

        return await Flip(map, version, cancellationToken, draft, replacing).ConfigureAwait(false);
    }

    /// <summary>
    /// Retires the current version (or, for a draft, deletes the open draft), makes this one current (or the draft) and adds the file's overrides, in one transaction that commits
    /// only when this version was still 'building' (another import's sweep may have removed it); otherwise nothing changes.
    /// </summary>
    internal async ValueTask<Result<long>> Flip(MapImport map, long version, CancellationToken cancellationToken, bool draft = false, int? replacing = null)
    {
        var begun = await sessions.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var building = await SqliteQueries.One(begun.Value, new SqlStatement(GraphSql.IsBuilding, c => c.Bind("$v", version)), GraphRows.Id, cancellationToken).ConfigureAwait(false);
            if (building.IsError || building.Value != 1)
            {
                return building.IsError ? ContextDatabase.Translate(building.Error)
                    : replacing is not null ? ContextErrors.PromoteRefused.With("the draft is no longer the open draft: another build removed the copy being written; nothing was replaced")
                    : ContextErrors.StoreUnavailable.With("another import removed this one before it became current; nothing changed (fix: import again)");
            }

            var switched = await SwitchOver(begun.Value, map, (version, draft, replacing), cancellationToken).ConfigureAwait(false);
            if (switched.IsError)
            {
                return switched.Error;
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : version;
        }
    }

    // The statements that make the version current (or the draft) and add the file's overrides; a replacement removes only the draft it read, and 046 when that is gone.
    private static async ValueTask<Result<Unit>> SwitchOver(ISqliteSession session, MapImport map, (long Version, bool Draft, int? Replacing) target, CancellationToken cancellationToken)
    {
        var (version, draft, replacing) = target;
        // The heads first: a replacement copies the replaced draft's before that draft (and its rows) is deleted.
        var heads = await Execute(session, HeadStatements(map, version, draft, replacing), cancellationToken).ConfigureAwait(false);
        if (heads.IsError)
        {
            return heads.Error;
        }

        if (replacing is { } source)
        {
            var removed = await SqliteQueries.Execute(session, new SqlStatement(GraphSql.DeleteDraft, c => c.Bind("$v", (long)source)), cancellationToken).ConfigureAwait(false);
            if (removed.IsError || removed.Value != 1)
            {
                return removed.IsError ? ContextDatabase.Translate(removed.Error)
                    : ContextErrors.PromoteRefused.With("the draft is no longer the open draft; nothing was replaced");
            }
        }

        SqlStatement[] switchOver = draft
            ? [.. replacing is null ? (SqlStatement[])[new(GraphSql.DeleteDrafts)] : [], new(GraphSql.MakeDraft, c => c.Bind("$v", version))]
            : [new(GraphSql.RetireCurrent), new(GraphSql.MakeCurrent, c => c.Bind("$v", version))];
        // A new current version ends any wait: the hold and the chain go with it (a draft's hold goes with the draft it names).
        SqlStatement[] clear = draft ? [] : [new(GraphSql.DeleteHolds), new(GraphSql.DeleteChain)];
        return await Execute(session, [.. switchOver, .. clear, .. map.Overrides.Select(GraphStatements.Override)], cancellationToken).ConfigureAwait(false);
    }

    // Runs the statements in order inside the session; the first failure is the result.
    private static async ValueTask<Result<Unit>> Execute(ISqliteSession session, IEnumerable<SqlStatement> statements, CancellationToken cancellationToken)
    {
        foreach (var statement in statements)
        {
            var done = await SqliteQueries.Execute(session, statement, cancellationToken).ConfigureAwait(false);
            if (done.IsError)
            {
                return ContextDatabase.Translate(done.Error);
            }
        }

        return Unit.Value;
    }

    /// <summary>Retires the current version and makes the draft current, in one transaction; anything but a draft is SND-CONTEXT-046.</summary>
    public async ValueTask<Result<MapVersion>> Promote(int draft, CancellationToken cancellationToken)
    {
        var begun = await sessions.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var isDraft = await SqliteQueries.One(begun.Value, new SqlStatement(GraphSql.VersionStatus, c => c.Bind("$v", (long)draft)), GraphRows.Id, cancellationToken).ConfigureAwait(false);
            if (isDraft.IsError || isDraft.Value != 1)
            {
                return isDraft.IsError ? ContextDatabase.Translate(isDraft.Error)
                    : ContextErrors.PromoteRefused.With(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"version {draft} isn't a draft (fix: list the versions with 'sindri context map status')"));
            }

            var switched = await Execute(begun.Value, [new(GraphSql.RetireCurrent), new(GraphSql.MakeCurrentDraft, c => c.Bind("$v", (long)draft)), new(GraphSql.DeleteHolds), new(GraphSql.DeleteChain)], cancellationToken).ConfigureAwait(false);
            if (switched.IsError)
            {
                return switched.Error;
            }

            var version = await SqliteQueries.One(begun.Value, new SqlStatement(GraphSql.SelectVersion, c => c.Bind("$v", (long)draft)), GraphRows.Version, cancellationToken).ConfigureAwait(false);
            if (version.IsError)
            {
                return ContextDatabase.Translate(version.Error);
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : version.Value;
        }
    }

    /// <summary>Adds a scoping run's 'propose' rows and replaces its surface list in one transaction (all or none).</summary>
    public async ValueTask<Result<int>> Propose(ScopeProposal proposal, IReadOnlyList<MapOverride> rows, CancellationToken cancellationToken)
    {
        var written = await Run([.. rows.Select(GraphStatements.Override), GraphStatements.Run(proposal)], returnsId: false, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : (int)written.Value - 1;   // the total counts the run's one upserted row
    }

    /// <summary>Remembers a scoping run's surface list, replacing an earlier one of the same run.</summary>
    public async ValueTask<Result<int>> SaveRun(ScopeProposal proposal, CancellationToken cancellationToken)
    {
        var saved = await Run([GraphStatements.Run(proposal)], returnsId: false, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : proposal.Surfaces.Count;
    }

    /// <summary>Removes a version that didn't become current, whatever the caller's token says; reports both failures if that fails too.</summary>
    private async ValueTask<Result<MapVersion>> Abandon(long version, Error? error)
    {
        var removed = await Run([new SqlStatement(GraphSql.DeleteBuildingVersion, c => c.Bind("$v", version))], returnsId: false, CancellationToken.None).ConfigureAwait(false);
        var cause = error is null ? "the save stopped" : $"{error.Code}: {error.Message}";
        return removed.IsError
            ? ContextErrors.StoreUnavailable.With(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{removed.Error.Code}: {removed.Error.Message}, while removing building version {version} after {cause}"))
            : error ?? ContextErrors.StoreUnavailable.With("the save stopped; nothing became current");
    }

    /// <summary>Runs <paramref name="statements"/> in one write transaction: the id the first returns, or the rows they changed.</summary>
    private async ValueTask<Result<long>> Run(IReadOnlyList<SqlStatement> statements, bool returnsId, CancellationToken cancellationToken)
    {
        var begun = await sessions.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            long total = 0;
            foreach (var statement in statements)
            {
                var step = returnsId
                    ? await SqliteQueries.One(begun.Value, statement, GraphRows.Id, cancellationToken).ConfigureAwait(false)
                    : (await SqliteQueries.Execute(begun.Value, statement, cancellationToken).ConfigureAwait(false)).Map(n => (long)n);
                if (step.IsError)
                {
                    return ContextDatabase.Translate(step.Error);
                }

                total = returnsId ? step.Value : total + step.Value;
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : total;
        }
    }
}
