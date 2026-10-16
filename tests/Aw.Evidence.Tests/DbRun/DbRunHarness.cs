using System.Text;
using System.Text.Json;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Features.DbRun;
using Aw.Evidence.Testing;
using Aw.Kernel.Services;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Evidence.Tests.DbRun;

/// <summary>The db-run slice over in-memory fakes: the script at a fixed path, a SQLite file or a Postgres entry, a fake reader.</summary>
internal sealed class DbRunHarness
{
    internal const string ScriptPath = "/work/db.json";
    internal const string RunDirectory = "/runs/db-1";
    internal const string Repo = "/work/repo";
    internal const string SqliteFile = "/work/repo/data/notes.db";

    internal static readonly DatabaseEntry PostgresEntry =
        new("postgres", "127.0.0.1", 55432, "fixture", "evidence_ro", "EVIDENCE_PG_SECRET", Disposable: true, new ServerIdentity("172.17.0.2", "fixture"));

    internal static readonly Rows OneNote = new(["id", "text"], [[1L, "hello"]], Truncated: false);

    internal InMemoryRunFiles Files { get; } = new();

    internal InMemoryMachineLocks Locks { get; } = new();

    internal FakeDatabaseReader Reader { get; } = new();

    internal FakeExecutableLocator Locator { get; } = new();

    internal FakeEvidenceTargets Targets { get; } = new();

    internal Dictionary<string, string> Environment { get; } = new(StringComparer.Ordinal);

    internal FakeTextScrubber Scrubber { get; } = new();

    /// <summary>When set, the run uses this scrubber (a real one) instead of <see cref="Scrubber"/>.</summary>
    internal ITextScrubber? RealScrubber { get; set; }

    internal SequentialIds Ids { get; } = new();

    internal FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero));

    internal static string SummaryPath => RunDirectory + "/summary.json";

    internal DbRunHarness WithScript(string json)
    {
        _ = Files.With(ScriptPath, Encoding.UTF8.GetBytes(json));
        return this;
    }

    /// <summary>The SQLite file exists in the repo and the repo and run directory are real directories.</summary>
    internal DbRunHarness WithSqliteFile(string? real = null)
    {
        _ = Locator.File(SqliteFile, real).Directory(Repo).Directory(RunDirectory);
        return this;
    }

    internal DbRunHarness WithPostgresEntry(DatabaseEntry? entry = null)
    {
        Targets.Targets = new ResolvedTargets([], [], [], new Dictionary<string, DatabaseEntry>(StringComparer.Ordinal) { ["fixture-pg"] = entry ?? PostgresEntry });
        Environment["EVIDENCE_PG_SECRET"] = "pg-value-for-the-fake";
        _ = Locator.Directory(Repo).Directory(RunDirectory);
        return this;
    }

    internal static string SqliteScript(string queries, string extra = "") =>
        $$"""{ {{extra}} "sqlite":"data/notes.db","queries":[{{queries}}]}""";

    internal static string Query(string sql, string expect) => $$"""{"sql":"{{sql}}","expect":{{expect}}}""";

    internal static DbRunRequest Request(DataRunAt at = DataRunAt.Head, string? appBuild = null) => new(ScriptPath, RunDirectory, at, TimeSpan.Zero, Repo, appBuild);

    internal ValueTask<Kernel.Result<DbRunResponse>> Run(DbRunRequest? request = null)
    {
        var runFiles = new DataRunFiles(Files, RealScrubber ?? Scrubber, TestJson.Options);
        var service = new ValidatingService<DbRunRequest, DbRunResponse>(
            new DbRunService(runFiles, new DbVetting(runFiles, Targets, Locator, new FakeSecrets(Environment)), new DbQueryRunner(Reader), Locks, Clock, Ids), new DbRunValidator());
        return service.Handle(request ?? Request(), TestContext.Current.CancellationToken);
    }

    internal static JsonElement WrittenSummaryOf(DbRunResponse response) => JsonDocument.Parse(response.Document).RootElement;
}
