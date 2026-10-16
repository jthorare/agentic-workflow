using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Tests.Data;

public sealed class TargetsReaderTests
{
    private static Kernel.Result<ResolvedTargets> Read(string json) => TargetsReader.Read(TestJson.Utf8(json), TestJson.Options);

    [Fact, Trait("Lane", "unit")]
    public void An_empty_object_allows_nothing()
    {
        var targets = Read("{}").Value;

        Assert.True(targets.AllowsNoProgram);
        Assert.Empty(targets.PassEnv);
    }

    [Fact, Trait("Lane", "unit")]
    public void Build_outputs_allow_and_pass_env_read_back_and_build_outputs_are_normalized()
    {
        var targets = Read("""{"schema":"evidence.targets/1","cli":{"buildOutputs":["out/","./build/bin"],"allow":["/opt/tool"],"passEnv":["LANG","NOTES_REGION"]}}""").Value;

        Assert.Equal(["out", "build/bin"], targets.BuildOutputs);
        Assert.Equal(["/opt/tool"], targets.Allow);
        Assert.Equal(["LANG", "NOTES_REGION"], targets.PassEnv);
    }

    private static string Databases(string entry) => """{"databases":{"fixture-pg":""" + entry + "}}";

    private const string Entry =
        """{"engine":"postgres","host":"127.0.0.1","port":55432,"database":"fixture","user":"evidence_ro","passwordEnv":"EVIDENCE_PG_SECRET","disposable":true,"identity":{"serverAddress":"172.17.0.2","serverPort":5432,"database":"fixture"}}""";

    [Fact, Trait("Lane", "unit")]
    public void A_databases_entry_reads_back_with_its_identity()
    {
        var entry = Read(Databases(Entry)).Value.DatabaseMap["fixture-pg"];

        Assert.Equal(("postgres", "127.0.0.1", 55432, "fixture", "evidence_ro", "EVIDENCE_PG_SECRET", true), (entry.Engine, entry.Host, entry.Port, entry.Database, entry.User, entry.PasswordEnv, entry.Disposable));
        Assert.Equal(new ServerIdentity("172.17.0.2", "fixture", 5432), entry.Identity);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_databases_is_an_empty_map()
        => Assert.Empty(Read("{}").Value.DatabaseMap);

    [Theory, Trait("Lane", "unit")]
    [InlineData("\"engine\":\"postgres\"", "\"engine\":\"mysql\"", "$.databases.fixture-pg.engine")]
    [InlineData("\"host\":\"127.0.0.1\"", "\"host\":\"/var/run/postgresql\"", "$.databases.fixture-pg.host")]
    [InlineData("\"port\":55432", "\"port\":0", "$.databases.fixture-pg.port")]
    [InlineData("\"port\":55432", "\"port\":70000", "$.databases.fixture-pg.port")]
    [InlineData("\"user\":\"evidence_ro\"", "\"user\":\"\"", "$.databases.fixture-pg.user")]
    [InlineData("\"passwordEnv\":\"EVIDENCE_PG_SECRET\"", "\"passwordEnv\":\"lower\"", "$.databases.fixture-pg.passwordEnv")]
    [InlineData("\"serverAddress\":\"172.17.0.2\"", "\"serverAddress\":\"\"", "$.databases.fixture-pg.identity.serverAddress")]
    [InlineData("\"serverPort\":5432", "\"serverPort\":0", "$.databases.fixture-pg.identity.serverPort")]
    [InlineData("\"disposable\":true", "\"disposable\":true,\"extra\":1", "extra")]
    public void A_databases_entry_breaking_a_rule_is_refused_with_its_location(string from, string to, string location)
    {
        var error = Read(Databases(Entry.Replace(from, to, StringComparison.Ordinal))).Error;

        Assert.Equal(EvidenceErrors.TargetsInvalid.Code, error.Code);
        Assert.Contains(location, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Targets_repo_file_cannot_hold_an_unknown_key_and_names_where()
    {
        var error = Read("""{"cli":{"buildOutputs":["out"],"allowed":[]}}""").Error;

        Assert.Equal(EvidenceErrors.TargetsInvalid.Code, error.Code);
        Assert.Contains("$.cli", error.Message, StringComparison.Ordinal);
        Assert.Contains("unknown property", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"cli":{"buildOutputs":["/abs"]}}""", "relative")]
    [InlineData("""{"cli":{"buildOutputs":[".."]}}""", "inside the repo")]
    [InlineData("""{"cli":{"buildOutputs":["a/../.."]}}""", "inside the repo")]
    [InlineData("""{"cli":{"buildOutputs":[""]}}""", "relative")]
    [InlineData("""{"cli":{"allow":["tool"]}}""", "absolute")]
    [InlineData("""{"cli":{"passEnv":["lower"]}}""", "UPPER_SNAKE")]
    [InlineData("""{"schema":"x/1"}""", "evidence.targets/1")]
    [InlineData("""{"databases":3}""", "$.databases")]
    public void A_file_breaking_a_rule_is_refused_with_the_rule_and_its_location(string json, string rule)
    {
        var error = Read(json).Error;

        Assert.Equal(EvidenceErrors.TargetsInvalid.Code, error.Code);
        Assert.StartsWith("$", error.Message, StringComparison.Ordinal);
        Assert.Contains(rule, error.Message, StringComparison.Ordinal);
    }
}
