using Aw.Evidence.Core.Data;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeDatabaseReaderContractTests : ReadOnlyDatabaseContract
{
    private static readonly (string Name, string Sql)[] _bypasses =
    [
        ("a writing CTE", "WITH x AS (INSERT INTO notes(text) VALUES ('w') RETURNING 1) SELECT * FROM x"),
        ("a side-effecting function", "SELECT bump()"),
        ("nextval", "SELECT nextval('notes_id_seq')"),
    ];

    protected override Task<DatabaseUnderTest> ArrangeAsync()
    {
        var notes = new Rows(["id", "text"], [[1L, "hello"], [2L, "world"], [3L, "again"]], Truncated: false);
        var typed = new Rows(["id", "text", "gone", "ratio"], [[1L, "hello", null, 1.5d]], Truncated: false);
        var fake = new FakeDatabaseReader().Answer(ReadAll, notes).Answer(ReadTyped, typed);
        foreach (var (_, sql) in _bypasses)
        {
            _ = fake.Refuse(sql);
        }

        return Task.FromResult(new DatabaseUnderTest(fake, new SqliteTarget("/src/notes.db", "/work"), () => Task.FromResult(string.Empty)));
    }

    protected override IReadOnlyList<(string Name, string Sql)> Bypasses() => _bypasses;
}
