using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.Data;

public sealed class WriteStatementsTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("INSERT INTO notes(text) VALUES ('x')")]
    [InlineData("update notes set text = 'y'")]
    [InlineData("DELETE FROM notes")]
    [InlineData("TRUNCATE notes")]
    [InlineData("DROP TABLE notes")]
    [InlineData("CREATE TABLE t(a int)")]
    [InlineData("ALTER TABLE notes ADD COLUMN b int")]
    [InlineData("COPY notes TO '/tmp/x'")]
    [InlineData("ATTACH 'new.db' AS n")]
    [InlineData("DETACH n")]
    [InlineData("VACUUM INTO 'copy.db'")]
    [InlineData("VACUUM")]
    [InlineData("PRAGMA user_version = 9")]
    [InlineData("pragma user_version(9)")]
    [InlineData("SET transaction_read_only = off")]
    [InlineData("BEGIN")]
    [InlineData("COMMIT")]
    [InlineData("WITH x AS (INSERT INTO notes(text) VALUES ('w') RETURNING 1) SELECT * FROM x")]
    [InlineData("WITH x AS (SELECT 1) INSERT INTO notes SELECT * FROM x")]
    [InlineData("SELECT 1; DELETE FROM notes")]
    [InlineData("/* harmless */ DELETE FROM notes")]
    [InlineData("-- a comment\nINSERT INTO notes DEFAULT VALUES")]
    public void WriteStatements_refuses_insert_update_delete_ddl_copy_attach_vacuum_and_pragma_assignment_before_connecting(string sql)
    {
        var result = WriteStatements.Check(sql, nestedComments: true);

        Assert.True(result.IsError, sql);
        Assert.Equal(EvidenceErrors.DatabaseRefused.Code, result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SELECT * FROM notes")]
    [InlineData("select count(*) from notes where text = 'insert into x'")]
    [InlineData("SELECT \"update\" FROM notes")]
    [InlineData("SELECT 1 -- delete from notes")]
    [InlineData("/* DROP TABLE x */ SELECT 1")]
    [InlineData("WITH x AS (SELECT 1 AS a) SELECT * FROM x")]
    [InlineData("VALUES (1), (2)")]
    [InlineData("TABLE notes")]
    [InlineData("SHOW server_version")]
    [InlineData("EXPLAIN SELECT * FROM notes")]
    [InlineData("PRAGMA table_info(notes)")]
    [InlineData("pragma index_list('notes')")]
    [InlineData("SELECT * FROM notes;")]
    [InlineData("SELECT $$ delete from x; $$ AS s")]
    public void WriteStatements_lets_a_read_through(string sql)
        => Assert.True(WriteStatements.Check(sql, nestedComments: true).IsSuccess, sql);

    [Theory, Trait("Lane", "unit")]
    [InlineData("SELECT bump()")]
    [InlineData("SELECT nextval('notes_id_seq')")]
    [InlineData("SELECT setval('notes_id_seq', 100)")]
    [InlineData("SELECT lo_import('/etc/passwd')")]
    public void WriteStatements_does_not_try_to_catch_functions(string sql)
        => Assert.True(WriteStatements.Check(sql, nestedComments: true).IsSuccess, sql);

    [Fact, Trait("Lane", "unit")]
    public void WriteStatements_names_the_statement_it_refused()
    {
        var result = WriteStatements.Check("COPY notes TO '/tmp/x'", nestedComments: true);

        Assert.Contains("COPY", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void WriteStatements_refuses_an_empty_query()
        => Assert.Equal(EvidenceErrors.DatabaseRefused.Code, WriteStatements.Check("  -- nothing\n ", nestedComments: true).Error.Code);
}
