using System.Text.Json.Nodes;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Tests.DbRun;

namespace Aw.Evidence.Tests.Schemas;

/// <summary>A summary db-run produced, and a script it accepts, validate against the committed schemas; a script it refuses does not.</summary>
public sealed class DbSchemaValidationTests
{
    private const string Read = "SELECT id, text FROM notes ORDER BY id";

    private static DbRunHarness With(string expect = """{"rowCount":1}""")
    {
        var harness = new DbRunHarness().WithScript(DbRunHarness.SqliteScript(DbRunHarness.Query(Read, expect))).WithSqliteFile();
        _ = harness.Reader.Answer(Read, DbRunHarness.OneNote);
        return harness;
    }

    private static IReadOnlyList<string> Violations(string schemaName, string document) =>
        MiniSchema.Violations(EvidenceSchemaTests.Committed(schemaName), JsonNode.Parse(document));

    [Fact, Trait("Lane", "integration")]
    public async Task A_passed_summary_validates_against_the_schema()
    {
        var response = (await With().Run()).Value;

        Assert.Equal(ApiOutcome.Passed, response.Summary.Outcome);
        Assert.Empty(Violations("db-summary", response.Document));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_failed_summary_with_its_failures_validates_against_the_schema()
    {
        var response = (await With("""{"rowCount":5}""").Run()).Value;

        Assert.NotEmpty(response.Summary.Failures);
        Assert.Empty(Violations("db-summary", response.Document));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_refused_summary_validates_against_the_schema()
    {
        var harness = new DbRunHarness().WithScript(DbRunHarness.SqliteScript(DbRunHarness.Query("DELETE FROM notes", """{"rowCount":1}"""))).WithSqliteFile();

        var response = (await harness.Run()).Value;

        Assert.NotNull(response.Summary.Refusal);
        Assert.Empty(Violations("db-summary", response.Document));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_errored_summary_validates_against_the_schema()
    {
        var harness = With();
        _ = harness.Reader.Fail(Read, "no such table: notes");

        var response = (await harness.Run()).Value;

        Assert.NotNull(response.Summary.Error);
        Assert.Empty(Violations("db-summary", response.Document));
    }

    [Fact, Trait("Lane", "integration")]
    public void The_schema_rejects_a_summary_that_lacks_a_key_or_names_an_unknown_outcome()
    {
        var violations = Violations("db-summary", """{"schema":"evidence.db.summary/1","outcome":"maybe"}""");

        Assert.Contains(violations, static v => v.Contains("outcome", StringComparison.Ordinal));
        Assert.Contains(violations, static v => v.Contains("missing required property", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public void A_valid_script_validates_and_a_script_with_an_unknown_key_does_not()
    {
        const string valid = """{"sqlite":"data/notes.db","seed":"seed.sql","rowLimit":10,"queries":[{"sql":"SELECT 1","expect":{"rowCount":1,"pointers":[{"pointer":"/0/a","equals":1}],"golden":"g.json"}}]}""";
        const string unknown = """{"sqlite":"a.db","queries":[{"sql":"SELECT 1","expect":{"rowCount":1,"rows":1}}]}""";

        Assert.Empty(Violations("db-script", valid));
        Assert.NotEmpty(Violations("db-script", unknown));
    }
}
