using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.Data;

public sealed class SqlTextTests
{
    [Fact, Trait("Lane", "unit")]
    public void SqlText_splits_at_top_level_semicolons_only()
    {
        var statements = SqlText.Statements("SELECT ';'; SELECT \"a;b\" /* ; */ FROM t -- ;\n; SELECT $tag$ ; $tag$; ;", nestedComments: true);

        Assert.Equal(3, statements.Count);
        Assert.Equal("SELECT ';'", statements[0]);
        Assert.StartsWith("SELECT \"a;b\"", statements[1], StringComparison.Ordinal);
        Assert.Equal("SELECT $tag$ ; $tag$", statements[2]);
    }

    [Fact, Trait("Lane", "unit")]
    public void SqlText_reads_words_outside_quotes_and_comments()
    {
        var words = SqlText.Words("select 'insert' as x, \"delete\" /* update */ from t -- drop\n where a_1 = 2");

        Assert.Equal(["SELECT", "AS", "X", "FROM", "T", "WHERE", "A_1"], [.. words.Take(7)]);
        Assert.DoesNotContain("INSERT", words, StringComparer.Ordinal);
        Assert.DoesNotContain("DROP", words, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void SqlText_nests_block_comments_for_postgres_and_not_for_sqlite()
    {
        const string Sql = "SELECT 1 /* /* */ ; VACUUM INTO 'x' /* */";

        _ = Assert.Single(SqlText.Statements(Sql, nestedComments: true));
        Assert.Equal(["SELECT 1", "VACUUM INTO 'x'"], SqlText.Statements(Sql, nestedComments: false));
        Assert.Equal(["SELECT 2"], SqlText.Statements("/* a /* b */ SELECT 2", nestedComments: false));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_write_check_for_sqlite_sees_the_statement_a_non_nesting_comment_hides_from_the_nesting_scan()
    {
        Assert.True(WriteStatements.Check("SELECT 1 /* /* */ ; VACUUM INTO 'x' /* */", nestedComments: false).IsError);
        Assert.False(WriteStatements.Check("SELECT 1 /* /* */ ; VACUUM INTO 'x' /* */", nestedComments: true).IsError);
    }
}
