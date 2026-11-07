using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The pickers' lists in context.db (migration 0032_s1l_linear_hub, Linear hub spec 2026-11-04 §4.2): a team's states, cycles, members and labels, replaced together in one transaction and read back by the team's key.</summary>
/// <param name="database">The context database.</param>
internal sealed class SqliteLinearLookupStore(ContextDatabase database) : ILinearLookupStore
{
    public async ValueTask<Result<Unit>> ReplaceTeam(LinearTeamLookups team, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            if (await Replace(begun.Value, team, cancellationToken).ConfigureAwait(false) is { } failure)
            {
                return failure;   // the session rolls back when it is disposed uncommitted
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            database.RestrictFiles();
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : Unit.Value;
        }
    }

    public ValueTask<Result<Unit>> ReplaceWorkspaceUrlKey(string urlKey, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<string?>> WorkspaceUrlKey(CancellationToken cancellationToken) => throw new NotSupportedException();

    public async ValueTask<Result<LinearTeamLookups?>> Team(string teamKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(teamKey);
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : Results.Success<LinearTeamLookups?>(value: null);
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            return await ReadTeam(read.Value, teamKey, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask<Result<LinearTeamLookups?>> ReadTeam(ISqliteSession session, string teamKey, CancellationToken cancellationToken)
    {
        var teams = await SqliteQueries.List(session, new SqlStatement(LinearLookupSql.TeamByKey, c => c.Bind("@key", teamKey)), TeamMapper, cancellationToken).ConfigureAwait(false);
        if (teams.IsError)
        {
            return ContextDatabase.Translate(teams.Error);
        }

        if (teams.Value.Count == 0)
        {
            return Results.Success<LinearTeamLookups?>(value: null);
        }

        var (id, key) = teams.Value[0];
        var states = await List(session, LinearLookupSql.States, id, StateMapper, cancellationToken).ConfigureAwait(false);
        var cycles = states.IsError ? states.Error : await List(session, LinearLookupSql.Cycles, id, CycleMapper, cancellationToken).ConfigureAwait(false);
        var members = cycles.IsError ? cycles.Error : await List(session, LinearLookupSql.Members, id, MemberMapper, cancellationToken).ConfigureAwait(false);
        var labels = members.IsError ? members.Error : await List(session, LinearLookupSql.Labels, id, LabelMapper, cancellationToken).ConfigureAwait(false);
        return labels.IsError ? labels.Error : Results.Success<LinearTeamLookups?>(new LinearTeamLookups(id, key, states.Value, cycles.Value, members.Value, labels.Value, Name: key, SyncedAt: default));
    }

    private static async ValueTask<Result<IReadOnlyList<T>>> List<T>(ISqliteSession session, string sql, ExternalId team, IRowMapper<T> mapper, CancellationToken cancellationToken)
    {
        var rows = await SqliteQueries.List(session, new SqlStatement(sql, c => c.Bind("@team", team.Value)), mapper, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows;
    }

    // why: the team's rows go and come back as one set, so a state, cycle, member or label Linear no longer lists is gone, and a team whose key changed keeps one row
    private static async ValueTask<Error?> Replace(ISqliteSession session, LinearTeamLookups team, CancellationToken cancellationToken)
    {
        var failure = await Exec(session, LinearLookupSql.DeleteTeam, c => c.Bind("@team", team.TeamId.Value).Bind("@key", team.TeamKey), cancellationToken).ConfigureAwait(false)
            ?? await Exec(session, LinearLookupSql.DeleteStates, c => c.Bind("@team", team.TeamId.Value), cancellationToken).ConfigureAwait(false)
            ?? await Exec(session, LinearLookupSql.DeleteCycles, c => c.Bind("@team", team.TeamId.Value), cancellationToken).ConfigureAwait(false)
            ?? await Exec(session, LinearLookupSql.DeleteMembers, c => c.Bind("@team", team.TeamId.Value), cancellationToken).ConfigureAwait(false)
            ?? await Exec(session, LinearLookupSql.DeleteLabels, c => c.Bind("@team", team.TeamId.Value), cancellationToken).ConfigureAwait(false)
            ?? await Exec(session, LinearLookupSql.InsertTeam, c => c.Bind("@team", team.TeamId.Value).Bind("@key", team.TeamKey), cancellationToken).ConfigureAwait(false);
        return failure ?? await Insert(session, team, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Error?> Insert(ISqliteSession session, LinearTeamLookups team, CancellationToken cancellationToken)
    {
        var id = team.TeamId.Value;
        foreach (var state in team.States)
        {
            var failure = await Exec(session, LinearLookupSql.InsertState, c => c.Bind("@team", id).Bind("@id", state.Id.Value).Bind("@name", state.Name).Bind("@type", state.Type).Bind("@position", state.Position), cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }
        }

        foreach (var cycle in team.Cycles)
        {
            var failure = await Exec(session, LinearLookupSql.InsertCycle, c => c.Bind("@team", id).Bind("@id", cycle.Id.Value).Bind("@number", cycle.Number).Bind("@name", cycle.Name).Bind("@starts", cycle.StartsAt).Bind("@ends", cycle.EndsAt), cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }
        }

        return await InsertPeople(session, team, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Error?> InsertPeople(ISqliteSession session, LinearTeamLookups team, CancellationToken cancellationToken)
    {
        var id = team.TeamId.Value;
        foreach (var member in team.Members)
        {
            var failure = await Exec(session, LinearLookupSql.InsertMember, c => c.Bind("@team", id).Bind("@id", member.Id.Value).Bind("@name", member.Name).Bind("@display", member.DisplayName), cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }
        }

        foreach (var label in team.Labels)
        {
            var failure = await Exec(session, LinearLookupSql.InsertLabel, c => c.Bind("@team", id).Bind("@id", label.Id.Value).Bind("@name", label.Name), cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    private static ValueTask<Error?> Exec(ISqliteSession session, string sql, Action<SqliteCommand> bind, CancellationToken cancellationToken) =>
        SqliteRecordStore.Exec(session, sql, bind, cancellationToken);

    private static string? Text(SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : r.GetString(ordinal);

    private static IRowMapper<(ExternalId Id, string Key)> TeamMapper { get; } = new Mapper<(ExternalId, string)>(r => (ExternalId.From(r.GetString(0)), r.GetString(1)));

    private static IRowMapper<LinearLookupState> StateMapper { get; } = new Mapper<LinearLookupState>(r => new LinearLookupState(ExternalId.From(r.GetString(0)), r.GetString(1), r.GetString(2), r.GetDouble(3)));

    private static IRowMapper<LinearLookupCycle> CycleMapper { get; } = new Mapper<LinearLookupCycle>(r =>
        new LinearLookupCycle(ExternalId.From(r.GetString(0)), r.GetInt32(1), Text(r, 2), SqliteRead.GetTimestamp(r, 3), SqliteRead.GetTimestamp(r, 4)));

    private static IRowMapper<LinearLookupMember> MemberMapper { get; } = new Mapper<LinearLookupMember>(r => new LinearLookupMember(ExternalId.From(r.GetString(0)), r.GetString(1), Text(r, 2), Active: true));

    private static IRowMapper<LinearLookupLabel> LabelMapper { get; } = new Mapper<LinearLookupLabel>(r => new LinearLookupLabel(ExternalId.From(r.GetString(0)), r.GetString(1), Color: string.Empty));

    private sealed class Mapper<T>(Func<SqliteDataReader, T> map) : IRowMapper<T>
    {
        public T Map(SqliteDataReader reader) => map(reader);
    }
}
