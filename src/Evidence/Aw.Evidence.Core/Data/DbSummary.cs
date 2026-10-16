using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Usage;

namespace Aw.Evidence.Core.Data;

/// <summary>A db-run's record, written to &lt;run-dir&gt;/summary.json (schema 'evidence.db.summary/1'). Every key is always written.</summary>
/// <param name="Schema">Always <see cref="SchemaName"/>.</param>
/// <param name="Kind">Always 'db'.</param>
/// <param name="RunId">The run (the run directory's name).</param>
/// <param name="RunInstance">This invocation, new every time.</param>
/// <param name="StartedAt">When the run started (UTC).</param>
/// <param name="FinishedAt">When it finished (UTC).</param>
/// <param name="AppBuild">The --app-build commit, when given.</param>
/// <param name="ScriptSha256">SHA-256 of the script file.</param>
/// <param name="At">The side the caller named.</param>
/// <param name="Engine">'sqlite' or 'postgres'; null when the script could not be read.</param>
/// <param name="Target">The connection name, or the SQLite path as the script gave it.</param>
/// <param name="Outcome">How the run ended; agrees with the exit code.</param>
/// <param name="Headline">One line, 200 characters at most, scrubbed.</param>
/// <param name="Steps">Every query.</param>
/// <param name="Failures">Every assertion that did not hold.</param>
/// <param name="Provenance">'seeded' when a seed ran against the copy, else 'unseeded'.</param>
/// <param name="SeedSha256">SHA-256 of the seed file when one ran.</param>
/// <param name="Refusal">Set when a guard or the database refused the run (exit 1).</param>
/// <param name="Error">Set when the run errored (exit 4).</param>
/// <param name="Invocations">Always empty: no model runs.</param>
public sealed record DbSummary(
    string Schema,
    string Kind,
    RunId RunId,
    RunInstanceId RunInstance,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string? AppBuild,
    string ScriptSha256,
    DataRunAt At,
    string? Engine,
    string? Target,
    ApiOutcome Outcome,
    string Headline,
    IReadOnlyList<DbStep> Steps,
    IReadOnlyList<ApiFailure> Failures,
    string Provenance,
    string? SeedSha256,
    CodedMessage? Refusal,
    CodedMessage? Error,
    IReadOnlyList<ModelInvocation> Invocations)
{
    /// <summary>The schema this build writes.</summary>
    public const string SchemaName = "evidence.db.summary/1";

    /// <summary>The 'kind' a db summary carries.</summary>
    public const string KindName = "db";

    /// <summary>The 'provenance' of a run that seeded nothing.</summary>
    public const string Unseeded = "unseeded";

    /// <summary>The 'provenance' of a run that seeded its copy.</summary>
    public const string Seeded = "seeded";

    /// <summary>The process exit code this summary's outcome stands for.</summary>
    /// <returns>0 passed, 2 failed, 1 refused, 4 errored.</returns>
    public int ExitCode() => Outcome switch
    {
        ApiOutcome.Passed => 0,
        ApiOutcome.Failed => 2,
        ApiOutcome.Refused => 1,
        ApiOutcome.Errored => 4,
        _ => 4,
    };
}
