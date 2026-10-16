using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.Data;

public sealed class DbScriptReaderTests
{
    private const string Valid =
        """{"schema":"evidence.db.run/1","sqlite":"data/notes.db","seed":"seed.sql","rowLimit":50,"queries":[""" +
        """{"sql":"SELECT text FROM notes ORDER BY id;","expect":{"rowCount":1,"pointers":[{"pointer":"/0/text","equals":"hello"}]}},""" +
        """{"sql":"SELECT count(*) AS n FROM notes","expect":{"golden":"count.json"}}]}""";

    private static Kernel.Result<DbScript> Read(string json) => DbScriptReader.Read(TestJson.Utf8(json), TestJson.Options);

    private static string One(string extra, string sql = "SELECT 1", string expect = """{"rowCount":1}""") =>
        $$"""{{{extra}}"queries":[{"sql":"{{sql}}","expect":{{expect}}}]}""";

    [Fact, Trait("Lane", "unit")]
    public void DbScript_reads_the_target_the_seed_the_limit_and_typed_queries()
    {
        var script = Read(Valid).Value;

        Assert.Equal("data/notes.db", script.Sqlite);
        Assert.Equal("seed.sql", script.Seed);
        Assert.Equal(50, script.EffectiveRowLimit);
        Assert.Equal(2, script.QueryList.Count);
        Assert.Equal(1, script.QueryList[0].Expect.RowCount);
        Assert.Equal("hello", script.QueryList[0].Expect.Pointers![0].EqualTo!.Value.GetString());
        Assert.Equal("count.json", script.QueryList[1].Expect.Golden);
    }

    [Fact, Trait("Lane", "unit")]
    public void DbScript_defaults_the_row_limit_to_200()
    {
        var script = Read(One("\"sqlite\":\"a.db\",")).Value;

        Assert.Equal(200, script.EffectiveRowLimit);
    }

    [Fact, Trait("Lane", "unit")]
    public void DbScript_refuses_an_unknown_key()
    {
        var result = Read(One("\"sqlite\":\"a.db\",", expect: """{"rowCount":1,"rows":2}"""));

        Assert.Equal(EvidenceErrors.DbScriptInvalid.Code, result.Error.Code);
        Assert.Contains("$.queries[0].expect", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("rows", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void DbScript_names_a_connection_or_a_sqlite_file_not_both()
    {
        var both = Read(One("\"connection\":\"fixture-pg\",\"sqlite\":\"a.db\","));
        var neither = Read(One(string.Empty));

        Assert.Equal(EvidenceErrors.DbScriptInvalid.Code, both.Error.Code);
        Assert.Contains("not both", both.Error.Message, StringComparison.Ordinal);
        Assert.Equal(EvidenceErrors.DbScriptInvalid.Code, neither.Error.Code);
        Assert.Contains("connection or sqlite", neither.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void DbScript_takes_a_seed_only_with_a_sqlite_file()
    {
        var result = Read(One("\"connection\":\"fixture-pg\",\"seed\":\"seed.sql\","));

        Assert.Equal(EvidenceErrors.DbScriptInvalid.Code, result.Error.Code);
        Assert.Contains("seed", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void DbScript_wants_one_statement_per_query_and_tolerates_a_trailing_semicolon_and_semicolons_in_strings()
    {
        var two = Read(One("\"sqlite\":\"a.db\",", sql: "SELECT 1; SELECT 2"));
        var fine = Read(One("\"sqlite\":\"a.db\",", sql: "SELECT ';' AS x -- done;\\n;"));

        Assert.Equal(EvidenceErrors.DbScriptInvalid.Code, two.Error.Code);
        Assert.Contains("$.queries[0].sql", two.Error.Message, StringComparison.Ordinal);
        Assert.True(fine.IsSuccess);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"queries":[]}""")]
    [InlineData("""{"sqlite":"a.db","queries":[]}""")]
    [InlineData("""{"sqlite":"a.db","queries":[{"sql":"","expect":{"rowCount":1}}]}""")]
    [InlineData("""{"sqlite":"a.db","queries":[{"sql":"SELECT 1","expect":{}}]}""")]
    [InlineData("""{"sqlite":"a.db","queries":[{"sql":"SELECT 1","expect":{"rowCount":-1}}]}""")]
    [InlineData("""{"sqlite":"a.db","queries":[{"sql":"SELECT 1","expect":{"golden":""}}]}""")]
    [InlineData("""{"sqlite":"a.db","rowLimit":0,"queries":[{"sql":"SELECT 1","expect":{"rowCount":1}}]}""")]
    [InlineData("""{"sqlite":"a.db","rowLimit":1001,"queries":[{"sql":"SELECT 1","expect":{"rowCount":1}}]}""")]
    [InlineData("""{"schema":"evidence.api.run/1","sqlite":"a.db","queries":[{"sql":"SELECT 1","expect":{"rowCount":1}}]}""")]
    [InlineData("""{"sqlite":"a.db","queries":[{"sql":"SELECT 1","expect":{"pointers":[{"pointer":"x","exists":true}]}}]}""")]
    [InlineData("""{"sqlite":"a.db","queries":[{"sql":"SELECT 1","expect":{"pointers":[{"pointer":"/a"}]}}]}""")]
    public void DbScript_refuses_each_rule_it_states(string json)
    {
        var result = Read(json);

        Assert.True(result.IsError);
        Assert.Equal(EvidenceErrors.DbScriptInvalid.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void DbScript_names_the_location_of_a_malformed_document()
    {
        var result = Read("""{"sqlite":"a.db","queries":[{"sql":5,"expect":{"rowCount":1}}]}""");

        Assert.Equal(EvidenceErrors.DbScriptInvalid.Code, result.Error.Code);
        Assert.Contains("$.queries[0].sql", result.Error.Message, StringComparison.Ordinal);
    }
}
