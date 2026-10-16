using System.Text.Json.Nodes;
using Aw.Evidence.Tests.DbRun;

namespace Aw.Evidence.Tests.Runs;

/// <summary>The db summary.json shape that evidence.md documents: every key is always written, in this order, and an unknown value is null (never left out).</summary>
public sealed class DbSummaryShapeTests
{
    private static readonly string[] _top =
    [
        "schema", "kind", "runId", "runInstance", "startedAt", "finishedAt", "appBuild", "scriptSha256", "at", "engine", "target", "outcome", "headline",
        "steps", "failures", "provenance", "seedSha256", "refusal", "error", "invocations",
    ];

    private static string[] Keys(JsonNode? node) => [.. Assert.IsType<JsonObject>(node).Select(static p => p.Key)];

    private static async Task<JsonObject> Passing()
    {
        const string sql = "SELECT id, text FROM notes";
        var harness = new DbRunHarness().WithScript(DbRunHarness.SqliteScript(DbRunHarness.Query(sql, """{"rowCount":1}"""))).WithSqliteFile();
        _ = harness.Reader.Answer(sql, DbRunHarness.OneNote);
        return Assert.IsType<JsonObject>(JsonNode.Parse((await harness.Run()).Value.Document));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_summary_writes_every_documented_key_in_order_and_null_for_each_unknown()
    {
        var json = await Passing();

        Assert.Equal(_top, Keys(json));
        Assert.Equal(["appBuild", "seedSha256", "refusal", "error"], [.. _top.Where(key => json[key] is null)]);
        Assert.Equal(["evidence.db.summary/1", "db", "unseeded", "passed", "sqlite"], [json["schema"]!.GetValue<string>(), json["kind"]!.GetValue<string>(), json["provenance"]!.GetValue<string>(), json["outcome"]!.GetValue<string>(), json["engine"]!.GetValue<string>()]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_writes_its_documented_keys()
    {
        var json = await Passing();

        Assert.Equal(["index", "action", "target", "status", "rowCount", "truncated", "output"], Keys(json["steps"]![0]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failure_and_a_refusal_write_their_documented_keys()
    {
        var failing = new DbRunHarness().WithScript(DbRunHarness.SqliteScript(DbRunHarness.Query("SELECT 1", """{"rowCount":2}"""))).WithSqliteFile();
        _ = failing.Reader.Answer("SELECT 1", DbRunHarness.OneNote);
        var refused = new DbRunHarness().WithScript(DbRunHarness.SqliteScript(DbRunHarness.Query("DELETE FROM notes", """{"rowCount":1}"""))).WithSqliteFile();

        var failed = JsonNode.Parse((await failing.Run()).Value.Document)!;
        var refusal = JsonNode.Parse((await refused.Run()).Value.Document)!;

        Assert.Equal(["step", "pointer", "expected", "actual"], Keys(failed["failures"]![0]));
        Assert.Equal(["code", "message"], Keys(refusal["refusal"]));
    }
}
