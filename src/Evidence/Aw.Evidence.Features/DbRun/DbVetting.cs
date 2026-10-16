using System.Text;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Guard;
using Aw.Kernel;

namespace Aw.Evidence.Features.DbRun;

/// <summary>The guards that run before any lock: the write statements, the database target, the goldens and the seed.</summary>
internal sealed class DbVetting(DataRunFiles files, IEvidenceTargets targets, IExecutableLocator locator, ISecretsSource secrets)
{
    internal const string WorkFolder = "db";

    internal async ValueTask<Result<VettedDb>> VetAsync(DbRunRequest request, DbScript script, CancellationToken cancellationToken)
    {
        // The targets config is read for every run (as api-run does), so a missing --config (010) or an invalid file (022) is reported for a SQLite script too.
        var loaded = await targets.LoadAsync(request.Repo, request.ConfigPath, cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var queries = CheckQueries(script);
        if (queries.IsError)
        {
            return queries.Error;
        }

        var inputs = await ReadInputsAsync(request, script, cancellationToken).ConfigureAwait(false);
        if (inputs.IsError)
        {
            return inputs.Error;
        }

        var (goldens, seed) = inputs.Value;
        var target = script.Connection is { } name ? Postgres(loaded.Value, name) : Sqlite(request, script, seed?.Text);
        return target.Map(t => new VettedDb(script, t, goldens, seed?.Hash, t is PostgresTarget p ? [p.Password] : []));
    }

    // The goldens and the seed text; a seed that reaches beyond the run's copy is refused here, before any lock.
    private async ValueTask<Result<(IReadOnlyList<string?> Goldens, SeedFile? Seed)>> ReadInputsAsync(DbRunRequest request, DbScript script, CancellationToken cancellationToken)
    {
        var goldens = await ReadGoldensAsync(request, script, cancellationToken).ConfigureAwait(false);
        var seed = await ReadSeedAsync(request.ScriptPath, script.Seed, cancellationToken).ConfigureAwait(false);
        if (goldens.IsError || seed.IsError)
        {
            return goldens.IsError ? goldens.Error : seed.Error;
        }

        var guarded = seed.Value is null ? Unit.Value : SeedStatements.Check(seed.Value.Text);
        return guarded.IsError ? guarded.Error with { Message = "seed: " + guarded.Error.Message } : (goldens.Value, seed.Value);
    }

    private static Result<Unit> CheckQueries(DbScript script)
    {
        for (var i = 0; i < script.QueryList.Count; i++)
        {
            var allowed = WriteStatements.Check(script.QueryList[i].Sql, nestedComments: script.Sqlite is null);
            if (allowed.IsError)
            {
                return allowed.Error with { Message = $"query {(i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}: {allowed.Error.Message}" };
            }
        }

        return Unit.Value;
    }

    private async ValueTask<Result<IReadOnlyList<string?>>> ReadGoldensAsync(DbRunRequest request, DbScript script, CancellationToken cancellationToken)
    {
        var goldens = new List<string?>();
        foreach (var query in script.QueryList)
        {
            var golden = await ReadTextAsync(request.ScriptPath, query.Expect.Golden, cancellationToken).ConfigureAwait(false);
            if (golden.IsError)
            {
                return golden.Error;
            }

            goldens.Add(golden.Value);
        }

        return goldens;
    }

    private Result<DbTarget> Sqlite(DbRunRequest request, DbScript script, string? seedSql)
    {
        var given = script.Sqlite ?? string.Empty;
        var path = given.StartsWith('/') ? given : RunPath.Combine(request.Repo, given);
        var real = locator.RealFile(path);
        if (real is null)
        {
            return EvidenceErrors.FileNotFound.With($"{path} is not a file");
        }

        var roots = new[] { locator.RealDirectory(request.Repo), locator.RealDirectory(request.RunDirectory) };
        if (!roots.Any(root => root is not null && real.StartsWith(root.TrimEnd('/') + "/", StringComparison.Ordinal)))
        {
            return EvidenceErrors.DatabaseRefused.With($"the SQLite file {real} is outside the repo {request.Repo} and the run directory; db-run reads files inside those only");
        }

        return new SqliteTarget(real, RunPath.Combine(locator.RealDirectory(request.RunDirectory) ?? request.RunDirectory, WorkFolder), seedSql);
    }

    private Result<DbTarget> Postgres(ResolvedTargets loaded, string name)
    {
        if (!loaded.DatabaseMap.TryGetValue(name, out var entry))
        {
            var known = loaded.DatabaseMap.Count == 0 ? "none" : string.Join(", ", loaded.DatabaseMap.Keys.Order(StringComparer.Ordinal));
            return EvidenceErrors.DatabaseRefused.With($"no database entry '{name}' in the targets config (it names: {known}); add it to <repo>/{TargetsFile.RepoRelativePath} or pass --config");
        }

        if (!entry.Disposable)
        {
            return EvidenceErrors.DatabaseRefused.With($"entry '{name}' is not marked disposable: db-run reads only a throwaway database");
        }

        return secrets.Get(entry.PasswordEnv) is { } login
            ? new PostgresTarget(entry, login)
            : EvidenceErrors.CredentialsMissing.With($"environment variable {entry.PasswordEnv} (database {name}) is not set");
    }

    // The seed's text for the connection and the hash of the file's own bytes.
    private async ValueTask<Result<SeedFile?>> ReadSeedAsync(string scriptPath, string? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return (SeedFile?)null;
        }

        var bytes = await files.ReadAsync(ScriptRelative(scriptPath, file), cancellationToken).ConfigureAwait(false);
        return bytes.Map(static b => (SeedFile?)new SeedFile(Encoding.UTF8.GetString(b), ContentHash.Sha256Hex(b)));
    }

    private static string ScriptRelative(string scriptPath, string file) =>
        file.StartsWith('/') ? file : RunPath.Combine(scriptPath[..scriptPath.LastIndexOf('/')], file);

    private sealed record SeedFile(string Text, string Hash);

    // A relative file resolves against the script's directory; a missing one is AW-EVIDENCE-010 before any lock.
    private async ValueTask<Result<string?>> ReadTextAsync(string scriptPath, string? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return (string?)null;
        }

        var bytes = await files.ReadAsync(ScriptRelative(scriptPath, file), cancellationToken).ConfigureAwait(false);
        return bytes.Map(static b => (string?)Encoding.UTF8.GetString(b));
    }
}
