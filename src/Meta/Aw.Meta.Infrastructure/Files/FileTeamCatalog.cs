using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// ITeamCatalog over the meta root: teams/&lt;name&gt;/ for named teams, the flat files for the legacy one. Migration renames the
/// legacy files into teams/&lt;its team name&gt; and leaves gates/ where it is (gate trees are registered with git by absolute path, and
/// the gate records in meta.db point at their step logs).
/// </summary>
/// <param name="layout">Which team this run acts on, and the meta root.</param>
/// <param name="sessions">The live Claude sessions, to tell which team has a live meta session.</param>
/// <param name="json">The AwJson options of the host's context.</param>
/// <param name="clock">The clock the databases are opened with.</param>
internal sealed class FileTeamCatalog(TeamLayout layout, ISessionDirectory sessions, JsonSerializerOptions json, TimeProvider clock) : ITeamCatalog
{
    private const string GatesDirectory = "gates";

    private readonly string _teamsDirectory = Path.Combine(layout.MetaRoot, "teams");
    private readonly string _legacyConfig = Path.Combine(layout.MetaRoot, "team.json");

    public async ValueTask<Result<IReadOnlyList<TeamSummary>>> List(CancellationToken cancellationToken)
    {
        var live = await sessions.Live(cancellationToken).ConfigureAwait(false);
        var running = live.IsSuccess ? live.Value : [];
        var teams = new List<TeamSummary>();
        foreach (var name in TeamResolver.Named(layout.MetaRoot))
        {
            var directory = Path.Combine(_teamsDirectory, name);
            teams.Add(await Summarize(new TeamSource(name, "named", directory, name == layout.Team && !layout.Legacy), running, cancellationToken).ConfigureAwait(false));
        }

        if (File.Exists(_legacyConfig))
        {
            var name = TeamResolver.LegacyTeam(_legacyConfig) ?? "(unnamed)";
            teams.Add(await Summarize(new TeamSource(name, "legacy", layout.MetaRoot, layout.Legacy), running, cancellationToken).ConfigureAwait(false));
        }

        return teams.OrderBy(t => t.Name, StringComparer.Ordinal).ThenBy(t => t.Layout, StringComparer.Ordinal).ToList();
    }

    public async ValueTask<Result<TeamMigration>> Migrate(bool dryRun, CancellationToken cancellationToken)
    {
        var plan = Plan(dryRun);
        if (plan.IsError || dryRun)
        {
            return plan;
        }

        // The hooks write to meta.db all the time: a team with a running meta or lane session is not moved.
        var live = await sessions.Live(cancellationToken).ConfigureAwait(false);
        var running = RunningSession(plan.Value.Team, live.IsSuccess ? live.Value : []);
        if (running is not null)
        {
            return MetaErrors.TeamMigrationRefused.With($"the session {running} is running; stop the team's sessions first");
        }

        Checkpoint(Path.Combine(layout.MetaRoot, MetaDatabase.FileName));
        return Move(plan.Value);
    }

    // A running session (not an offline one) named <team>-<lane> for a lane of the legacy config, or one whose id a lane-sessions record holds.
    private string? RunningSession(string team, IReadOnlyList<Core.Lanes.LiveSession> live)
    {
        var names = new HashSet<string>(Read(_legacyConfig)?.Lanes.Select(lane => team + "-" + lane) ?? [], StringComparer.Ordinal);
        var lanes = Path.Combine(layout.MetaRoot, "lane-sessions");
        var recorded = new HashSet<string>(StringComparer.Ordinal);
        if (Directory.Exists(lanes))
        {
            foreach (var file in Directory.EnumerateFiles(lanes).Where(f => !Path.GetFileName(f).Contains('.', StringComparison.Ordinal)))
            {
                try
                {
                    _ = recorded.Add(File.ReadAllText(file).Trim());
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // An unreadable record can't name a session.
                }
            }
        }

        // An offline entry (a Remote Control session nothing runs for) is not a running session, whatever its name.
        var found = live.FirstOrDefault(s => !string.Equals(s.Status, "offline", StringComparison.OrdinalIgnoreCase) && (names.Contains(s.Name) || recorded.Contains(s.Session.Value)));
        return found is null ? null : found.Name.Length > 0 ? found.Name : found.Session.Value;
    }

    // Folds the WAL into meta.db and releases it, so the three files move as one consistent database.
    private static void Checkpoint(string database)
    {
        if (!File.Exists(database))
        {
            return;
        }

        try
        {
            using var connection = new SqliteConnection($"Data Source={database};Mode=ReadWrite;Pooling=False");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
            _ = command.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
            // A database that can't be checkpointed is moved as it is: its WAL moves with it.
        }
    }

    private Result<TeamMigration> Plan(bool dryRun)
    {
        if (!File.Exists(_legacyConfig))
        {
            return MetaErrors.NoTeamConfig.With($"no legacy team config at {_legacyConfig}; nothing to migrate");
        }

        var name = TeamResolver.LegacyTeam(_legacyConfig);
        if (name is null || !TeamLayout.IsValidName(name))
        {
            return MetaErrors.TeamMigrationRefused.With($"{_legacyConfig} has no team name usable as a directory (lowercase letters, digits and '-')");
        }

        var target = Path.Combine(_teamsDirectory, name);
        if (Directory.Exists(target) || File.Exists(target))
        {
            return MetaErrors.TeamMigrationRefused.With($"{target} already exists; move or remove it first");
        }

        var entries = Directory.EnumerateFileSystemEntries(layout.MetaRoot).Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal).ToList();
        // The database first and team.json last: until the last move the legacy config still names the flat layout.
        var moved = entries.Where(e => e is not ("teams" or GatesDirectory)).OrderBy(e => e.StartsWith(MetaDatabase.FileName, StringComparison.Ordinal) ? 0 : e == "team.json" ? 2 : 1).ThenBy(e => e, StringComparer.Ordinal).ToList();
        var left = entries.Where(e => e == GatesDirectory).Select(e => e + " (not moved: after the migration the sweep roots are teams/<team>/gates, so sweep these gate trees with worktree-sweep before migrating; they are throwaway)").ToList();
        return new TeamMigration(name, layout.MetaRoot, target, moved, left, dryRun);
    }

    // Moves the entries one by one; on a failure puts back what moved (reporting anything it cannot restore) and removes the empty target.
    private static Result<TeamMigration> Move(TeamMigration plan)
    {
        var done = new List<string>();
        var teamsExisted = Directory.Exists(Path.GetDirectoryName(plan.To));
        try
        {
            _ = Directory.CreateDirectory(plan.To);
            foreach (var entry in plan.Moved)
            {
                Relocate(Path.Combine(plan.From, entry), Path.Combine(plan.To, entry));
                done.Add(entry);
            }

            return plan;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var stuck = RestoreAll(plan, done);
            TryDeleteEmpty(plan.To);
            if (!teamsExisted)
            {
                TryDeleteEmpty(Path.GetDirectoryName(plan.To));
            }

            return MetaErrors.TeamMigrationRefused.With(
                $"moving into {plan.To} failed ({exception.Message}); "
                + (stuck.Count == 0 ? "the entries already moved were put back" : $"could not put back: {string.Join(", ", stuck)} (they are in {plan.To})"));
        }
    }

    private static List<string> RestoreAll(TeamMigration plan, List<string> done)
    {
        var stuck = new List<string>();
        foreach (var entry in done)
        {
            try
            {
                Relocate(Path.Combine(plan.To, entry), Path.Combine(plan.From, entry));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                stuck.Add(entry);
            }
        }

        return stuck;
    }

    private static void TryDeleteEmpty(string? directory)
    {
        try
        {
            if (directory is not null && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Leaving an empty directory behind is harmless to the files; the next migrate reports it.
        }
    }

    private static void Relocate(string from, string to)
    {
        if (Directory.Exists(from))
        {
            Directory.Move(from, to);
        }
        else
        {
            File.Move(from, to);
        }
    }

    private sealed record TeamSource(string Name, string Kind, string Directory, bool Selected);

    private async ValueTask<TeamSummary> Summarize(TeamSource source, IReadOnlyList<Core.Lanes.LiveSession> running, CancellationToken cancellationToken)
    {
        var (name, kind, directory, selected) = source;
        var config = Read(Path.Combine(directory, "team.json"));
        var role = await Role(directory, cancellationToken).ConfigureAwait(false);
        var isLive = role is not null && running.Any(s => s.Session == role.Session || (role.Name.Length > 0 && s.Name == role.Name));
        return new TeamSummary(name, kind, config?.Repo ?? "(invalid config)", config?.Lanes ?? [], role is null ? null : role.Name.Length > 0 ? role.Name : role.Session.Value, isLive, selected);
    }

    private TeamConfig? Read(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var config = AwJson.Deserialize<TeamConfig>(bytes.AsSpan().StartsWith("﻿"u8) ? bytes.AsSpan(3) : bytes, json);
            return config.IsError ? null : config.Value;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Only a database that is already there is opened: listing never creates a team's state.
    private async ValueTask<Core.Messages.MetaRole?> Role(string directory, CancellationToken cancellationToken)
    {
        if (!File.Exists(Path.Combine(directory, MetaDatabase.FileName)))
        {
            return null;
        }

        var role = await new SqliteMessageStore(new MetaDatabase(MetaDatabase.OptionsFor(directory), clock)).Role(cancellationToken).ConfigureAwait(false);
        return role.IsSuccess ? role.Value : null;
    }
}
