using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Stack;
using Aw.Evidence.Infrastructure.Processes;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Stack;

/// <summary>
/// Reads user emails from the configured seed table (its email column) with psql inside the configured container. The
/// seed pattern travels as a psql variable (-v pattern=…) and the SQL on standard input, where psql interpolates
/// :'pattern' as a quoted literal; psql doesn't interpolate variables in a -c command, so the pattern never becomes part
/// of the SQL text. The table name is spliced in, so DatabaseSettings only ever holds a plain [schema.]table identifier.
/// A worktree slot's database goes to psql as -d; without one psql connects to the user's default database.
/// With no container or no table configured every query fails, so provenance stays unknown.
/// </summary>
internal sealed class DockerPsqlProvenance(string docker, DatabaseSettings database) : IProvenanceQuery
{
    private static readonly TimeSpan _limit = TimeSpan.FromSeconds(15);

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> EmailsLikeAsync(string likePattern, string? database, CancellationToken cancellationToken) =>
        QueryAsync(static table => $"SELECT email FROM {table} WHERE email LIKE :'pattern';\n", database, ["-v", "pattern=" + likePattern], cancellationToken);

    // A NULL email is read as '' (never dropped): it isn't on a seed domain, so the check fails closed.
    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> AllEmailsAsync(string? database, CancellationToken cancellationToken) =>
        QueryAsync(static table => $"SELECT coalesce(email, '') FROM {table};\n", database, [], cancellationToken);

    // docker exec -i (standard input carries the SQL), the slot's database when there is one, unaligned tuples only, stop on the first error.
    private IReadOnlyList<string> Command(string container, string? slotDatabase, IReadOnlyList<string> variables) =>
        [docker, "exec", "-i", container, "psql", "-U", database.User, .. slotDatabase is null ? [] : new[] { "-d", slotDatabase }, "-At", "-v", "ON_ERROR_STOP=1", .. variables];

    private async ValueTask<Result<IReadOnlyList<string>>> QueryAsync(Func<string, string> sqlFor, string? slotDatabase, IReadOnlyList<string> variables, CancellationToken cancellationToken)
    {
        if (database.Container is not { } container)
        {
            return EvidenceErrors.ProbeFailed.With($"no database container configured (set {DatabaseSettings.ContainerVariable})");
        }

        if (database.SeedTable is not { } table)
        {
            return EvidenceErrors.ProbeFailed.With($"no seed table configured (set {DatabaseSettings.SeedTableVariable} to a plain [schema.]table name)");
        }

        var sql = sqlFor(table);
        var run = await ProcessRunner.RunAsync(Command(container, slotDatabase, variables), sql, _limit, EvidenceErrors.ProbeFailed, cancellationToken).ConfigureAwait(false);
        return run.Bind(outcome => outcome.ExitCode == 0
            ? Results.Success(Rows(outcome.StandardOutput))
            : EvidenceErrors.ProbeFailed.With(string.Create(CultureInfo.InvariantCulture, $"psql failed (exit {outcome.ExitCode}): {outcome.StandardError.Trim()}")));
    }

    // One row per line (psql -At); an empty line is a row whose value is empty, so only the final newline is dropped.
    private static IReadOnlyList<string> Rows(string output)
    {
        var text = output.Replace("\r\n", "\n", StringComparison.Ordinal);
        text = text.EndsWith('\n') ? text[..^1] : text;
        return text.Length == 0 ? [] : [.. text.Split('\n').Select(static row => row.Trim())];
    }
}
