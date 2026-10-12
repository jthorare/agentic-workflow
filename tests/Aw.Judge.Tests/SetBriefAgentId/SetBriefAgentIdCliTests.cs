namespace Aw.Judge.Tests.SetBriefAgentId;

// The contract of 'judge brief set-agent-id' (TS runBriefSetAgentId: prints nothing, exit 0; an unknown id is a no-op).
public sealed class SetBriefAgentIdCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Set_agent_id_maps_the_brief_so_get_shows_the_agent_and_map_by_name_skips_it()
    {
        var cli = new JudgeCli(_state);
        _ = await cli.RunWithInput(
            """{"toolUseId":"toolu_a","sessionId":"s","promptId":"p","dispatchName":"helper","subagentType":"x","goal":"g","acceptanceCriteria":"a","proofCommand":"c"}""",
            "brief", "save");

        Assert.Equal(new CliRun(0, "", ""), await cli.Run("brief", "set-agent-id", "toolu_a", "agent-7"));

        Assert.Equal("agent-7", (await cli.Run("brief", "get", "toolu_a")).Json().GetProperty("agentId").GetString());
        Assert.Equal(new CliRun(0, "", ""), await cli.Run("brief", "map-by-name", "helper"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Set_agent_id_on_an_unknown_tool_use_id_prints_nothing_and_exits_0()
    {
        var cli = new JudgeCli(_state);

        Assert.Equal(new CliRun(0, "", ""), await cli.Run("brief", "set-agent-id", "toolu_none", "agent-7"));
    }

    public void Dispose() => _state.Dispose();
}
