using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Infrastructure.Databases;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>
/// The Postgres reader, called directly (the pre-run check is not in front of it), against a throwaway container. Heavy lane: it needs
/// docker and the postgres:15.2-alpine image that is already on the box.
/// </summary>
[Lane(TestLane.Heavy)]
public sealed class PostgresDatabaseReaderTests(PostgresContainer container) : ReadOnlyDatabaseContract, IClassFixture<PostgresContainer>
{
    private DatabaseEntry Entry(string user = "evidence_ro", string? address = null) =>
        new("postgres", "127.0.0.1", container.Port, PostgresContainer.Database, user, "EVIDENCE_PG_SECRET", Disposable: true, new ServerIdentity(address ?? container.ServerAddress, PostgresContainer.Database));

    private PostgresTarget Target(DatabaseEntry entry, string? secret = null) => new(entry, secret ?? container.ReaderSecret);

    protected override async Task<DatabaseUnderTest> ArrangeAsync()
    {
        await container.ExecuteAsync("TRUNCATE notes RESTART IDENTITY; INSERT INTO notes(text) VALUES ('hello'), ('world'), ('again')");
        return new DatabaseUnderTest(new PostgresDatabaseReader(), Target(Entry()), Snapshot);
    }

    protected override IReadOnlyList<(string Name, string Sql)> Bypasses() =>
    [
        ("a writing CTE", "WITH x AS (INSERT INTO notes(text) VALUES ('w') RETURNING id) SELECT * FROM x"),
        ("a side-effecting function", "SELECT bump()"),
        ("nextval", "SELECT nextval('notes_id_seq')"),
        ("COPY ... TO a file", "COPY notes TO '/tmp/dte2-copy.csv'"),
        ("SET transaction_read_only = off", "SET transaction_read_only = off"),
        ("COMMIT; INSERT", "COMMIT; INSERT INTO notes(text) VALUES ('c')"),
        ("a delete", "DELETE FROM notes"),
        ("DDL", "CREATE TABLE t(a int)"),
    ];

    [Fact]
    public async Task A_query_runs_as_the_read_only_role_in_a_read_only_transaction()
    {
        await using var session = (await new PostgresDatabaseReader().OpenAsync(Target(Entry()), TestContext.Current.CancellationToken)).Value;

        var who = await session.QueryAsync("SELECT current_user AS who, current_setting('transaction_read_only') AS ro", 5, TestContext.Current.CancellationToken);

        Assert.Equal("""[{"who":"evidence_ro","ro":"on"}]""", who.Value.ToJson());
    }

    [Fact]
    public async Task A_column_that_reads_a_table_column_reports_the_table_column_s_name_and_a_computed_one_none()
    {
        await using var session = (await new PostgresDatabaseReader().OpenAsync(Target(Entry()), TestContext.Current.CancellationToken)).Value;

        var rows = await session.QueryAsync("SELECT api_key AS copy_of_key, 'key is ' || api_key AS sentence FROM secrets", 5, TestContext.Current.CancellationToken);

        Assert.Equal(["api_key", null], rows.Value.SourceColumns);
    }

    [Fact]
    public async Task A_server_message_that_quotes_a_value_does_not_carry_the_value()
    {
        await using var session = (await new PostgresDatabaseReader().OpenAsync(Target(Entry()), TestContext.Current.CancellationToken)).Value;

        var failed = await session.QueryAsync("SELECT api_key::uuid FROM secrets", 5, TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.RunFailed.Code, failed.Error.Code);
        Assert.Contains("22P02", failed.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ak-PLANTED-pgcol-123456", failed.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_server_message_that_quotes_a_value_with_a_double_quote_does_not_carry_its_tail()
    {
        await using var session = (await new PostgresDatabaseReader().OpenAsync(Target(Entry()), TestContext.Current.CancellationToken)).Value;

        var failed = await session.QueryAsync("SELECT 'ab\"cdefgh-tail-secret'::uuid", 5, TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.RunFailed.Code, failed.Error.Code);
        Assert.DoesNotContain("cdefgh-tail-secret", failed.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_join_with_the_same_column_name_twice_and_two_unnamed_columns_come_back_with_unique_keys()
    {
        await using var session = (await new PostgresDatabaseReader().OpenAsync(Target(Entry()), TestContext.Current.CancellationToken)).Value;

        var rows = await session.QueryAsync("SELECT a.id, b.id, 1, 2 FROM notes a JOIN notes b ON a.id = b.id ORDER BY a.id", 5, TestContext.Current.CancellationToken);

        Assert.True(rows.IsSuccess, rows.IsError ? rows.Error.Message : "");
        Assert.StartsWith("""[{"id":1,"id_2":1,"?column?":1,"?column?_2":2}""", rows.Value.ToJson(), StringComparison.Ordinal);
        Assert.NotEmpty(RecordedText.Of(new Evidence.Infrastructure.Redaction.RedactionScrubber(Redaction.Redactor.Default), rows.Value.ToJson(), 2000));
    }

    [Fact]
    public async Task A_postgres_date_and_time_come_back_in_iso()
    {
        await using var session = (await new PostgresDatabaseReader().OpenAsync(Target(Entry()), TestContext.Current.CancellationToken)).Value;

        var rows = await session.QueryAsync("SELECT '2020-01-31'::date AS d, '13:05:09'::time AS t", 5, TestContext.Current.CancellationToken);

        Assert.Equal("""[{"d":"2020-01-31","t":"13:05:09"}]""", rows.Value.ToJson());
    }

    [Fact]
    public async Task A_value_npgsql_cannot_map_to_an_object_is_named_by_its_type_and_the_run_goes_on()
    {
        await using var session = (await new PostgresDatabaseReader().OpenAsync(Target(Entry()), TestContext.Current.CancellationToken)).Value;

        var rows = await session.QueryAsync("SELECT 'NaN'::numeric AS n, '0044-03-15 BC'::date AS d, 7 AS seven", 5, TestContext.Current.CancellationToken);
        var after = await session.QueryAsync(ReadAll, 5, TestContext.Current.CancellationToken);

        Assert.True(rows.IsSuccess, rows.IsError ? rows.Error.Message : "");
        Assert.Contains("numeric: not representable", rows.Value.ToJson(), StringComparison.Ordinal);
        Assert.Contains("\"d\":\"\\u003Cdate: not representable\\u003E\"", rows.Value.ToJson(), StringComparison.Ordinal);
        Assert.Contains("\"seven\":7", rows.Value.ToJson(), StringComparison.Ordinal);
        Assert.Equal(3, after.Value.Count);
    }

    [Fact]
    public async Task A_bytea_and_a_text_array_come_back_as_bytes_and_strings()
    {
        await using var session = (await new PostgresDatabaseReader().OpenAsync(Target(Entry()), TestContext.Current.CancellationToken)).Value;

        var rows = await session.QueryAsync("SELECT secret_key, api_tokens FROM blobs", 5, TestContext.Current.CancellationToken);

        Assert.Equal("""[{"secret_key":"\\xdeadbeef1234","api_tokens":["tok-one-123456","tok-two-123456"]}]""", rows.Value.ToJson());
    }

    [Fact]
    public async Task A_select_the_role_may_not_read_is_a_failed_query_not_a_refused_write()
    {
        await using var session = (await new PostgresDatabaseReader().OpenAsync(Target(Entry()), TestContext.Current.CancellationToken)).Value;

        var failed = await session.QueryAsync("SELECT * FROM hidden", 5, TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.RunFailed.Code, failed.Error.Code);
        Assert.Contains("42501", failed.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("write", failed.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_server_that_is_not_the_one_named_is_refused_before_any_script_query()
    {
        var before = Count(await container.LogAsync(), "notes");

        var opened = await new PostgresDatabaseReader().OpenAsync(Target(Entry(address: "10.255.255.1")), TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.IdentityMismatch.Code, opened.Error.Code);
        Assert.Equal(before, Count(await container.LogAsync(), "notes"));
    }

    [Fact]
    public async Task A_database_that_is_not_the_one_named_is_refused()
    {
        var entry = Entry() with { Identity = new ServerIdentity(container.ServerAddress, "elsewhere") };

        var opened = await new PostgresDatabaseReader().OpenAsync(Target(entry), TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.IdentityMismatch.Code, opened.Error.Code);
    }

    [Theory]
    [InlineData("postgres", "superuser")]
    [InlineData("evidence_rw", "INSERT, UPDATE, DELETE or TRUNCATE")]
    [InlineData("evidence_files", "pg_write_server_files")]
    [InlineData("evidence_member", "INSERT, UPDATE, DELETE or TRUNCATE")]
    public async Task A_role_that_can_write_is_refused_before_any_script_query(string user, string reason)
    {
        var secret = user == "postgres" ? container.AdminSecret : container.ReaderSecret;

        var opened = await new PostgresDatabaseReader().OpenAsync(Target(Entry(user), secret), TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.DatabaseRefused.Code, opened.Error.Code);
        Assert.Contains(reason, opened.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_wrong_secret_is_unavailable_and_the_message_does_not_carry_it()
    {
        var opened = await new PostgresDatabaseReader().OpenAsync(Target(Entry(), "not-the-one"), TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.DatabaseUnavailable.Code, opened.Error.Code);
        Assert.DoesNotContain("not-the-one", opened.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_port_nothing_listens_on_is_unavailable()
    {
        var entry = Entry() with { Port = 1 };

        var opened = await new PostgresDatabaseReader().OpenAsync(Target(entry), TestContext.Current.CancellationToken);

        Assert.Equal(EvidenceErrors.DatabaseUnavailable.Code, opened.Error.Code);
    }

    // What a bypass could have changed: the rows, the sequence, and the file COPY ... TO would have written.
    private async Task<string> Snapshot() =>
        string.Join('|', await container.ScalarAsync("SELECT count(*) FROM notes"), await container.ScalarAsync("SELECT last_value || '/' || is_called FROM notes_id_seq"), await container.FileExistsAsync("/tmp/dte2-copy.csv"));

    private static int Count(string log, string word) => log.Split('\n').Count(line => line.Contains(word, StringComparison.Ordinal));
}
