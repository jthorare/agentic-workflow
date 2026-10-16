using System.Security.Cryptography;
using System.Text;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.DbRun;

public sealed class DbRunServiceTests
{
    private static readonly string[] _tokens = ["tok-one-123456", "tok-two-123456"];

    private const string ReadNotes = "SELECT id, text FROM notes ORDER BY id";

    private static string Count(int n) => """{"rowCount":""" + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";

    private static DbRunHarness Sqlite(string expect = """{"rowCount":1,"pointers":[{"pointer":"/0/text","equals":"hello"}]}""", string sql = ReadNotes)
    {
        var harness = new DbRunHarness().WithScript(DbRunHarness.SqliteScript(DbRunHarness.Query(sql, expect))).WithSqliteFile();
        _ = harness.Reader.Answer(sql, DbRunHarness.OneNote);
        return harness;
    }

    private static DbRunHarness Postgres(string sql = ReadNotes, string expect = """{"rowCount":1}""")
    {
        var harness = new DbRunHarness().WithScript($$"""{"connection":"fixture-pg","queries":[{{DbRunHarness.Query(sql, expect)}}]}""").WithPostgresEntry();
        _ = harness.Reader.Answer(sql, DbRunHarness.OneNote);
        return harness;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_sqlite_asserts_rows_a_command_wrote()
    {
        var harness = Sqlite();

        var response = (await harness.Run(DbRunHarness.Request(DataRunAt.Base, appBuild: "abc123"))).Value;

        var summary = response.Summary;
        Assert.Equal(ApiOutcome.Passed, summary.Outcome);
        Assert.Equal(0, summary.ExitCode());
        Assert.Equal(("sqlite", "data/notes.db", DataRunAt.Base, "abc123"), (summary.Engine, summary.Target, summary.At, summary.AppBuild));
        Assert.Equal(DbStepStatus.Passed, summary.Steps[0].Status);
        Assert.Equal(1, summary.Steps[0].RowCount);
        Assert.Contains("hello", summary.Steps[0].Output, StringComparison.Ordinal);
        Assert.Equal("passed: 2 assertions", summary.Headline);
        Assert.Equal(DbSummary.Unseeded, summary.Provenance);
        var opened = Assert.IsType<SqliteTarget>(Assert.Single(harness.Reader.Opened));
        Assert.Equal((DbRunHarness.SqliteFile, DbRunHarness.RunDirectory + "/db"), (opened.SourcePath, opened.WorkDirectory));
        Assert.True(harness.Files.Files.ContainsKey(DbRunHarness.SummaryPath));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_row_count_and_a_pointer_that_do_not_hold_fail_the_run_with_the_first_in_the_headline()
    {
        var harness = Sqlite("""{"rowCount":2,"pointers":[{"pointer":"/0/text","equals":"bye"}]}""");

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal((ApiOutcome.Failed, 2), (summary.Outcome, summary.ExitCode()));
        Assert.Equal(DbStepStatus.Failed, summary.Steps[0].Status);
        Assert.Equal(2, summary.Failures.Count);
        Assert.Null(summary.Failures[0].Location);
        Assert.Equal("/0/text", summary.Failures[1].Location);
        Assert.StartsWith("query 1: rowCount 1, expected 2 (assertion 1 of 2)", summary.Headline, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_the_rows_cut_by_the_limit_are_marked_truncated()
    {
        var harness = Sqlite(Count(1));
        _ = harness.Reader.Answer(ReadNotes, new Rows(["id"], [[1L], [2L]], Truncated: true));

        var summary = (await harness.Run()).Value.Summary;

        Assert.True(summary.Steps[0].Truncated);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("INSERT INTO notes(text) VALUES ('x')", "INSERT")]
    [InlineData("DELETE FROM notes", "DELETE")]
    [InlineData("VACUUM INTO '/tmp/c.db'", "VACUUM")]
    [InlineData("ATTACH '/tmp/n.db' AS n", "ATTACH")]
    [InlineData("PRAGMA user_version = 9", "PRAGMA")]
    public async Task DbRun_refuses_a_write_statement_before_connecting_or_taking_a_lock(string sql, string word)
    {
        var harness = Sqlite(sql: sql);

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal((ApiOutcome.Refused, 1), (summary.Outcome, summary.ExitCode()));
        Assert.Equal(EvidenceErrors.DatabaseRefused.Code, summary.Refusal!.Code);
        Assert.Contains(word, summary.Refusal.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Reader.Opened);
        Assert.Empty(summary.Steps);
        Assert.Empty(harness.Locks.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_function_that_writes_is_left_to_the_connection_and_a_refused_write_is_recorded_refused_exit_1()
    {
        var script = DbRunHarness.SqliteScript(DbRunHarness.Query(ReadNotes, Count(1)) + "," + DbRunHarness.Query("SELECT bump()", Count(1)) + "," + DbRunHarness.Query(ReadNotes, Count(1)));
        var harness = new DbRunHarness().WithScript(script).WithSqliteFile();
        _ = harness.Reader.Answer(ReadNotes, DbRunHarness.OneNote).Refuse("SELECT bump()");

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal((ApiOutcome.Refused, 1), (summary.Outcome, summary.ExitCode()));
        Assert.Equal([DbStepStatus.Passed, DbStepStatus.Refused, DbStepStatus.Skipped], summary.Steps.Select(static s => s.Status));
        Assert.Equal(EvidenceErrors.DatabaseRefused.Code, summary.Refusal!.Code);
        Assert.StartsWith("refused: AW-EVIDENCE-020", summary.Headline, StringComparison.Ordinal);
        Assert.Equal(["SELECT id, text FROM notes ORDER BY id", "SELECT bump()"], harness.Reader.Queries);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_query_that_fails_for_another_reason_is_broken_and_the_run_errored_exit_4()
    {
        var harness = Sqlite();
        _ = harness.Reader.Fail(ReadNotes, "no such table: notes");

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal((ApiOutcome.Errored, 4), (summary.Outcome, summary.ExitCode()));
        Assert.Equal(DbStepStatus.Broken, summary.Steps[0].Status);
        Assert.Equal(EvidenceErrors.RunFailed.Code, summary.Error!.Code);
        Assert.Contains("no such table", summary.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_sqlite_file_outside_repo_and_run_dir_is_refused()
    {
        var harness = Sqlite();
        _ = harness.WithSqliteFile(real: "/etc/elsewhere/notes.db").Locator.Directory(DbRunHarness.Repo, DbRunHarness.Repo);

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal(ApiOutcome.Refused, summary.Outcome);
        Assert.Equal(EvidenceErrors.DatabaseRefused.Code, summary.Refusal!.Code);
        Assert.Empty(harness.Reader.Opened);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_sqlite_file_that_is_not_there_is_file_not_found_exit_1()
    {
        var harness = new DbRunHarness().WithScript(DbRunHarness.SqliteScript(DbRunHarness.Query(ReadNotes, Count(1))));
        _ = harness.Locator.Directory(DbRunHarness.Repo).Directory(DbRunHarness.RunDirectory);

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal((ApiOutcome.Refused, 1), (summary.Outcome, summary.ExitCode()));
        Assert.Equal(EvidenceErrors.FileNotFound.Code, summary.Refusal!.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_postgres_reads_through_a_disposable_entry_with_its_password_from_the_environment()
    {
        var harness = Postgres();

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal((ApiOutcome.Passed, "postgres", "fixture-pg"), (summary.Outcome, summary.Engine, summary.Target));
        var opened = Assert.IsType<PostgresTarget>(Assert.Single(harness.Reader.Opened));
        Assert.Equal(("evidence_ro", "pg-value-for-the-fake"), (opened.Entry.User, opened.Password));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_postgres_entry_not_disposable_is_refused()
    {
        var harness = Postgres();
        _ = harness.WithPostgresEntry(DbRunHarness.PostgresEntry with { Disposable = false });

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal(EvidenceErrors.DatabaseRefused.Code, summary.Refusal!.Code);
        Assert.Contains("disposable", summary.Refusal.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Reader.Opened);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_postgres_connection_that_no_entry_names_is_refused()
    {
        var harness = new DbRunHarness().WithScript("""{"connection":"unlisted","queries":[{"sql":"SELECT 1","expect":{"rowCount":1}}]}""").WithPostgresEntry();

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal(EvidenceErrors.DatabaseRefused.Code, summary.Refusal!.Code);
        Assert.Contains("unlisted", summary.Refusal.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Reader.Opened);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_postgres_with_no_value_in_the_password_variable_is_refused_naming_the_variable_only()
    {
        var harness = Postgres();
        _ = harness.Environment.Remove("EVIDENCE_PG_SECRET");

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal(EvidenceErrors.CredentialsMissing.Code, summary.Refusal!.Code);
        Assert.Contains("EVIDENCE_PG_SECRET", summary.Refusal.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_postgres_identity_mismatch_refuses_before_any_query()
    {
        var harness = Postgres();
        harness.Reader.OpenError = EvidenceErrors.IdentityMismatch.With("expected the server at address 172.17.0.2, but it reports address 172.17.0.9");

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal((ApiOutcome.Refused, 1), (summary.Outcome, summary.ExitCode()));
        Assert.Equal(EvidenceErrors.IdentityMismatch.Code, summary.Refusal!.Code);
        Assert.Empty(harness.Reader.Queries);
        Assert.Empty(summary.Steps);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_postgres_superuser_role_is_refused()
    {
        var harness = Postgres();
        harness.Reader.OpenError = EvidenceErrors.DatabaseRefused.With("role postgres is not read-only: it is a superuser");

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal(EvidenceErrors.DatabaseRefused.Code, summary.Refusal!.Code);
        Assert.Contains("superuser", summary.Refusal.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Reader.Queries);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_database_that_cannot_be_reached_is_unavailable_exit_4()
    {
        var harness = Postgres();
        harness.Reader.OpenError = EvidenceErrors.DatabaseUnavailable.With("can't use 127.0.0.1:55432 as evidence_ro: connection refused");

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal((ApiOutcome.Errored, 4), (summary.Outcome, summary.ExitCode()));
        Assert.Equal(EvidenceErrors.DatabaseUnavailable.Code, summary.Error!.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_hides_a_secret_named_column_and_the_resolved_password_wherever_they_appear()
    {
        var harness = Postgres(expect: """{"rowCount":1}""");
        _ = harness.Scrubber.HidesName("api_key");
        _ = harness.Reader.Answer(ReadNotes, new Rows(["id", "api_key", "note"], [[1L, "ak-12345678", "echo pg-value-for-the-fake end"]], Truncated: false));

        var response = (await harness.Run()).Value;

        Assert.DoesNotContain("ak-12345678", response.Document, StringComparison.Ordinal);
        Assert.DoesNotContain("pg-value-for-the-fake", response.Document, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:secret-field]", response.Summary.Steps[0].Output, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_password_equal_to_a_fixed_field_scrubs_the_free_text_and_leaves_the_field_intact()
    {
        var harness = Postgres(expect: """{"rowCount":1}""");
        harness.Environment["EVIDENCE_PG_SECRET"] = "fixture-pg";
        _ = harness.Reader.Answer(ReadNotes, new Rows(["id", "note"], [[1L, "echo fixture-pg end"]], Truncated: false));

        var response = (await harness.Run()).Value;

        Assert.Equal("fixture-pg", response.Summary.Target);
        Assert.Equal(ApiOutcome.Passed, response.Summary.Outcome);
        Assert.Contains("[REDACTED:env-value]", response.Summary.Steps[0].Output, StringComparison.Ordinal);
        Assert.DoesNotContain("echo fixture-pg", response.Document, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_seed_that_attaches_or_vacuums_is_refused_before_any_lock_or_connection()
    {
        var harness = new DbRunHarness().WithScript(DbRunHarness.SqliteScript(DbRunHarness.Query(ReadNotes, Count(1)), "\"seed\":\"seed.sql\",")).WithSqliteFile();
        _ = harness.Files.With("/work/seed.sql", Encoding.UTF8.GetBytes("ATTACH '/work/repo/data/notes.db' AS src; INSERT INTO src.notes(text) VALUES ('x');"));

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal((ApiOutcome.Refused, 1), (summary.Outcome, summary.ExitCode()));
        Assert.Equal(EvidenceErrors.DatabaseRefused.Code, summary.Refusal!.Code);
        Assert.Contains("seed", summary.Refusal.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Reader.Opened);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_sqlite_still_reads_the_targets_config_so_a_missing_or_invalid_one_is_reported()
    {
        var harness = Sqlite();

        _ = await harness.Run();

        _ = Assert.Single(harness.Targets.Loads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_hides_a_secret_value_under_another_name_when_the_column_is_traced_to_a_secret_named_one()
    {
        var harness = Postgres(expect: """{"rowCount":1}""");
        _ = harness.Scrubber.HidesName("api_key");
        _ = harness.Reader.Answer(ReadNotes, new Rows(["copy_of_key", "sentence"], [["ak-PLANTED-pgcol-123456", "key is ak-PLANTED-pgcol-123456!"]], Truncated: false, SourceColumns: ["api_key", null]));

        var response = (await harness.Run()).Value;

        Assert.DoesNotContain("ak-PLANTED-pgcol-123456", response.Document, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_repeated_secret_column_keeps_hiding_under_its_renamed_key()
    {
        var harness = Sqlite();
        _ = harness.Scrubber.HidesName("cookie");
        _ = harness.Reader.Answer(ReadNotes, new Rows(["cookie", "cookie"], [["c1", "c2"]], Truncated: false));

        var response = (await harness.Run()).Value;

        Assert.Contains("\"cookie_2\":\"[REDACTED:secret-field]\"", response.Summary.Steps[0].Output, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_hides_a_secret_column_s_value_inside_a_computed_column_of_the_same_result()
    {
        var harness = Sqlite();
        _ = harness.Scrubber.HidesName("api_key");
        _ = harness.Reader.Answer(ReadNotes, new Rows(["api_key", "sentence"], [["ak-PLANTED-col-123456", "key is ak-PLANTED-col-123456"]], Truncated: false));

        var response = (await harness.Run()).Value;

        Assert.DoesNotContain("ak-PLANTED-col-123456", response.Document, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_seeds_only_into_the_run_copy_never_through_the_evidence_connection()
    {
        var harness = new DbRunHarness().WithScript(DbRunHarness.SqliteScript(DbRunHarness.Query(ReadNotes, Count(1)), "\"seed\":\"seed.sql\",")).WithSqliteFile();
        _ = harness.Files.With("/work/seed.sql", Encoding.UTF8.GetBytes("INSERT INTO notes(text) VALUES ('seeded');"));
        _ = harness.Reader.Answer(ReadNotes, DbRunHarness.OneNote);

        var summary = (await harness.Run()).Value.Summary;

        var opened = Assert.IsType<SqliteTarget>(Assert.Single(harness.Reader.Opened));
        Assert.Equal("INSERT INTO notes(text) VALUES ('seeded');", opened.SeedSql);
        Assert.Equal(DbRunHarness.SqliteFile, opened.SourcePath);
        Assert.DoesNotContain(harness.Reader.Queries, static q => q.Contains("seeded", StringComparison.Ordinal));
        Assert.Equal(ApiOutcome.Passed, summary.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_records_the_seed_sha256_as_provenance()
    {
        var harness = new DbRunHarness().WithScript(DbRunHarness.SqliteScript(DbRunHarness.Query(ReadNotes, Count(1)), "\"seed\":\"seed.sql\",")).WithSqliteFile();
        var seed = Encoding.UTF8.GetBytes("INSERT INTO notes(text) VALUES ('seeded');");
        _ = harness.Files.With("/work/seed.sql", seed);
        _ = harness.Reader.Answer(ReadNotes, DbRunHarness.OneNote);

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal((DbSummary.Seeded, Convert.ToHexStringLower(SHA256.HashData(seed))), (summary.Provenance, summary.SeedSha256));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_seed_file_that_is_missing_is_a_refusal_naming_the_file()
    {
        var harness = new DbRunHarness().WithScript(DbRunHarness.SqliteScript(DbRunHarness.Query(ReadNotes, Count(1)), "\"seed\":\"missing.sql\",")).WithSqliteFile();

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal(ApiOutcome.Refused, summary.Outcome);
        Assert.Equal(EvidenceErrors.FileNotFound.Code, summary.Refusal!.Code);
        Assert.Contains("missing.sql", summary.Refusal.Message, StringComparison.Ordinal);
        Assert.Empty(harness.Reader.Opened);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_hides_the_bytes_of_a_secret_named_blob_column_under_any_alias()
    {
        var harness = Sqlite("""{"rowCount":1}""");
        _ = harness.Scrubber.HidesName("password_hash");
        _ = harness.Reader.Answer(ReadNotes, new Rows(["h"], [[Convert.FromHexString("DEADBEEF1234")]], Truncated: false, SourceColumns: ["password_hash"]));

        var response = (await harness.Run()).Value;

        Assert.DoesNotContain("deadbeef1234", response.Document, StringComparison.OrdinalIgnoreCase);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_hides_every_element_of_a_secret_named_array_column_under_any_alias()
    {
        var harness = Postgres(expect: """{"rowCount":1}""");
        _ = harness.Scrubber.HidesName("api_tokens");
        _ = harness.Reader.Answer(ReadNotes, new Rows(["t", "when"], [[_tokens, new DateTime(2026, 10, 15, 9, 0, 0, DateTimeKind.Utc)]], Truncated: false, SourceColumns: ["api_tokens", null]));

        var response = (await harness.Run()).Value;

        Assert.DoesNotContain("tok-one-123456", response.Document, StringComparison.Ordinal);
        Assert.DoesNotContain("tok-two-123456", response.Document, StringComparison.Ordinal);
        Assert.DoesNotContain("System.String[]", response.Document, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_hashes_the_seed_file_s_own_bytes_even_when_they_are_not_valid_text()
    {
        var harness = new DbRunHarness().WithScript(DbRunHarness.SqliteScript(DbRunHarness.Query(ReadNotes, Count(1)), "\"seed\":\"seed.sql\",")).WithSqliteFile();
        byte[] seed = [.. Encoding.UTF8.GetBytes("SELECT 1; -- "), 0xFF, 0xFE];
        _ = harness.Files.With("/work/seed.sql", seed);
        _ = harness.Reader.Answer(ReadNotes, DbRunHarness.OneNote);

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(seed)), summary.SeedSha256);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_checks_a_golden_file_that_sits_next_to_the_script()
    {
        var harness = Sqlite("""{"golden":"gold.json"}""");
        _ = harness.Files.With("/work/gold.json", Encoding.UTF8.GetBytes("""[ { "id": 1, "text": "hello" } ]"""));

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal(ApiOutcome.Passed, summary.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_golden_file_that_differs_fails_the_run_exit_2_naming_the_file()
    {
        var harness = Sqlite("""{"golden":"gold.json"}""");
        _ = harness.Files.With("/work/gold.json", Encoding.UTF8.GetBytes("""[{"id":1,"text":"bye"}]"""));

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal((ApiOutcome.Failed, 2), (summary.Outcome, summary.ExitCode()));
        Assert.Contains("gold.json", Assert.Single(summary.Failures).Expected, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_missing_golden_file_is_a_refusal_before_any_lock_or_connection()
    {
        var harness = Sqlite("""{"golden":"nowhere.json"}""");

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal(EvidenceErrors.FileNotFound.Code, summary.Refusal!.Code);
        Assert.Empty(harness.Reader.Opened);
        Assert.Empty(harness.Locks.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_golden_path_resolves_against_the_script_s_directory_and_an_absolute_one_stands()
    {
        var harness = Sqlite("""{"golden":"/elsewhere/gold.json"}""");
        _ = harness.Files.With("/elsewhere/gold.json", Encoding.UTF8.GetBytes("""[{"id":1,"text":"hello"}]"""));

        var absolute = (await harness.Run()).Value.Summary;
        var relative = Sqlite("""{"golden":"sub/gold.json"}""");
        _ = relative.Files.With("/work/sub/gold.json", Encoding.UTF8.GetBytes("""[{"id":1,"text":"hello"}]"""));

        Assert.Equal(ApiOutcome.Passed, absolute.Outcome);
        Assert.Equal(ApiOutcome.Passed, (await relative.Run()).Value.Summary.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_row_count_that_holds_and_a_pointer_that_fails_counts_the_pointer_as_assertion_2_of_2()
    {
        var harness = Sqlite("""{"rowCount":1,"pointers":[{"pointer":"/0/text","equals":"bye"}]}""");

        var summary = (await harness.Run()).Value.Summary;

        Assert.Contains("(assertion 2 of 2)", summary.Headline, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_value_learned_from_a_secret_column_is_hidden_in_a_later_query_s_failure()
    {
        var harness = Postgres(expect: """{"rowCount":1}""");
        _ = harness.Scrubber.HidesName("api_key");
        _ = harness.Reader.Answer(ReadNotes, new Rows(["api_key"], [["ak-PLANTED-two-query-1"]], Truncated: false));
        const string second = "SELECT text FROM notes";
        _ = harness.WithScript($$"""{"connection":"fixture-pg","queries":[{{DbRunHarness.Query(ReadNotes, """{"rowCount":1}""")}},{{DbRunHarness.Query(second, """{"pointers":[{"pointer":"/0/x","equals":"ak-PLANTED-two-query-1"}]}""")}}]}""");
        _ = harness.Reader.Answer(second, new Rows(["x"], [["ak-PLANTED-two-query-1"]], Truncated: false));

        var response = (await harness.Run()).Value;

        Assert.DoesNotContain("ak-PLANTED-two-query-1", response.Document, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_an_invalid_script_is_a_refusal_with_the_script_code_and_a_summary()
    {
        var harness = new DbRunHarness().WithScript("""{"sqlite":"a.db","queries":[{"sql":"SELECT 1","expect":{"rowcount":1}}]}""");

        var summary = (await harness.Run()).Value.Summary;

        Assert.Equal(EvidenceErrors.DbScriptInvalid.Code, summary.Refusal!.Code);
        Assert.Contains("$.queries[0].expect", summary.Refusal.Message, StringComparison.Ordinal);
        Assert.Equal((ApiOutcome.Refused, "unseeded"), (summary.Outcome, summary.Provenance));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_lock_that_stays_busy_is_the_timeout_error_and_writes_no_summary()
    {
        var harness = Sqlite();
        _ = harness.Locks.AlwaysBusy.Add(Core.Locks.LockKind.QaStack);

        var result = await harness.Run();

        Assert.Equal(Kernel.ErrorKind.Timeout, result.Error.Kind);
        Assert.False(harness.Files.Files.ContainsKey(DbRunHarness.SummaryPath));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_takes_the_locks_for_the_queries_and_releases_them()
    {
        var harness = Sqlite();

        _ = await harness.Run();

        Assert.Contains("acquire QaStack", harness.Locks.Events, StringComparer.Ordinal);
        Assert.False(harness.Locks.IsHeld(Core.Locks.LockKind.QaStack));
        Assert.False(harness.Locks.IsHeld(Core.Locks.LockKind.HeavyJob));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_result_with_the_same_column_name_twice_is_recorded_with_unique_keys_on_both_engines()
    {
        foreach (var harness in new[] { Sqlite("""{"rowCount":1,"pointers":[{"pointer":"/0/id_2","equals":2}]}"""), Postgres(expect: """{"rowCount":1,"pointers":[{"pointer":"/0/id_2","equals":2}]}""") })
        {
            _ = harness.Scrubber.HidesName("password");
            _ = harness.Reader.Answer(ReadNotes, new Rows(["id", "id", "password"], [[1L, 2L, "pw-value-123456"]], Truncated: false));

            var result = await harness.Run();

            Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : "");
            var summary = result.Value.Summary;
            Assert.Equal(ApiOutcome.Passed, summary.Outcome);
            Assert.Contains("\"id_2\":2", summary.Steps[0].Output, StringComparison.Ordinal);
            Assert.DoesNotContain("pw-value-123456", result.Value.Document, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_records_a_date_and_a_time_in_iso_and_hides_a_secret_named_date_in_that_form()
    {
        var harness = Sqlite("""{"rowCount":1}""");
        _ = harness.Scrubber.HidesName("birth_secret");
        _ = harness.Reader.Answer(ReadNotes, new Rows(["birth_secret", "born", "at", "note"], [[new DateOnly(2020, 1, 31), new DateOnly(2021, 2, 3), new TimeOnly(13, 5, 9), "was born 2020-01-31"]], Truncated: false));

        var response = (await harness.Run()).Value;

        Assert.Contains("\"born\":\"2021-02-03\"", response.Summary.Steps[0].Output, StringComparison.Ordinal);
        Assert.Contains("\"at\":\"13:05:09\"", response.Summary.Steps[0].Output, StringComparison.Ordinal);
        Assert.DoesNotContain("2020-01-31", response.Document, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_with_the_real_name_rule_hides_a_hashed_password_column_and_a_plural_token_array()
    {
        var harness = Postgres(expect: """{"rowCount":1}""");
        harness.RealScrubber = new Evidence.Infrastructure.Redaction.RedactionScrubber(Redaction.Redactor.Default);
        _ = harness.Reader.Answer(ReadNotes, new Rows(["password_hash", "api_tokens", "plain"], [[Convert.FromHexString("DEADBEEF1234"), _tokens, "visible"]], Truncated: false));

        var response = (await harness.Run()).Value;

        Assert.DoesNotContain("deadbeef1234", response.Document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tok-one-123456", response.Document, StringComparison.Ordinal);
        Assert.DoesNotContain("tok-two-123456", response.Document, StringComparison.Ordinal);
        Assert.Contains("visible", response.Document, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_numeric_secret_value_does_not_stop_a_short_secret_named_string_being_hidden()
    {
        var harness = Postgres(expect: """{"rowCount":1}""");
        harness.RealScrubber = new Evidence.Infrastructure.Redaction.RedactionScrubber(Redaction.Redactor.Default);
        _ = harness.Reader.Answer(ReadNotes, new Rows(["reset_token", "password", "n"], [[123456L, "abc", 1234567L]], Truncated: false));

        var output = (await harness.Run()).Value.Summary.Steps[0].Output;

        Assert.DoesNotContain("abc", output, StringComparison.Ordinal);
        Assert.DoesNotContain("123456", output, StringComparison.Ordinal);
        Assert.Contains("\"password\":\"[REDACTED:secret-field]\"", output, StringComparison.Ordinal);
        Assert.Contains("\"reset_token\":\"[REDACTED:secret-field]\"", output, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_a_failed_pointer_into_a_renamed_repeated_secret_column_hides_both_values()
    {
        var harness = Sqlite();
        _ = harness.Scrubber.HidesName("cookie");
        _ = harness.WithScript("""{"sqlite":"data/notes.db","queries":[{"sql":"SELECT 1","expect":{"pointers":[{"pointer":"/0/cookie_2","equals":"expected-PLANTED-9"}]}}]}""");
        _ = harness.Reader.Answer("SELECT 1", new Rows(["cookie", "cookie"], [["c-one-111", "actual-PLANTED-8"]], Truncated: false));

        var response = (await harness.Run()).Value;

        Assert.DoesNotContain("expected-PLANTED-9", response.Document, StringComparison.Ordinal);
        Assert.DoesNotContain("actual-PLANTED-8", response.Document, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task DbRun_gives_the_reader_a_work_directory_under_the_run_directory_s_real_path()
    {
        var harness = Sqlite();
        _ = harness.Locator.Directory(DbRunHarness.RunDirectory, "/private/runs/db-1");

        _ = await harness.Run();

        var target = Assert.IsType<SqliteTarget>(Assert.Single(harness.Reader.Opened));
        Assert.StartsWith("/private/runs/db-1/", target.WorkDirectory, StringComparison.Ordinal);
    }
}
