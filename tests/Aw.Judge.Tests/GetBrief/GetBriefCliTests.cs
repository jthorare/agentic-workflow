using System.Text.Json;

namespace Aw.Judge.Tests.GetBrief;

// The frozen surface of 'judge brief get' (TS runBriefGet: the brief as JSON, camelCase fields).
public sealed class GetBriefCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Brief_get_prints_the_saved_brief_with_the_contract_field_names()
    {
        var cli = new JudgeCli(_state);
        cli.Clock.Advance(TimeSpan.FromMilliseconds(1234.5678));
        _ = await cli.RunWithInput(
            """{"toolUseId":"toolu_1","sessionId":"s1","promptId":"p1","dispatchName":"helper","subagentType":"Explore","goal":"Find \"it\"","acceptanceCriteria":"é\n😀","proofCommand":""}""",
            "brief", "save");

        var run = await cli.Run("brief", "get", "toolu_1");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        var json = run.Json();
        Assert.Equal("toolu_1", json.GetProperty("toolUseId").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("agentId").ValueKind);
        Assert.Equal("s1", json.GetProperty("sessionId").GetString());
        Assert.Equal("p1", json.GetProperty("promptId").GetString());
        Assert.Equal("helper", json.GetProperty("dispatchName").GetString());
        Assert.Equal("Explore", json.GetProperty("subagentType").GetString());
        Assert.Equal("Find \"it\"", json.GetProperty("goal").GetString());
        Assert.Equal("é\n😀", json.GetProperty("acceptanceCriteria").GetString());
        Assert.Equal("", json.GetProperty("proofCommand").GetString());
        Assert.Equal(cli.Clock.GetUtcNow(), json.GetProperty("savedAt").GetDateTimeOffset());
        CliGolden.Match(run, "GetBrief/Golden/brief-get.json");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Brief_get_exits_1_when_no_brief_was_saved()
    {
        var cli = new JudgeCli(_state);

        Assert.Equal(new CliRun(1, "", "no brief for tool_use_id: toolu_none\n"), await cli.Run("brief", "get", "toolu_none"));
    }

    public void Dispose() => _state.Dispose();
}
