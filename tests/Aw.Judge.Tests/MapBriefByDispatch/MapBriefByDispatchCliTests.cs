namespace Aw.Judge.Tests.MapBriefByDispatch;

// The contract of 'judge brief map-by-dispatch' (TS runBriefMapByDispatch: the tool use id or nothing, exit 0).
public sealed class MapBriefByDispatchCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Map_by_dispatch_prints_the_oldest_unmapped_brief_of_the_dispatch()
    {
        var cli = new JudgeCli(_state);
        await Save(cli, "toolu_a", "Plan");
        cli.Clock.Advance(TimeSpan.FromSeconds(1));
        await Save(cli, "toolu_b", "Explore");
        cli.Clock.Advance(TimeSpan.FromSeconds(1));
        await Save(cli, "toolu_c", "Explore");

        Assert.Equal(new CliRun(0, "toolu_b\n", ""), await cli.Run("brief", "map-by-dispatch", "s1", "p1", "Explore"));
        Assert.Equal(new CliRun(0, "", ""), await cli.Run("brief", "map-by-dispatch", "s1", "p2", "Explore"));
    }

    private static async Task Save(JudgeCli cli, string toolUseId, string type) =>
        Assert.Equal(0, (await cli.RunWithInput(
            $$"""{"toolUseId":"{{toolUseId}}","sessionId":"s1","promptId":"p1","dispatchName":null,"subagentType":"{{type}}","goal":"g","acceptanceCriteria":"a","proofCommand":"c"}""",
            "brief", "save")).ExitCode);

    public void Dispose() => _state.Dispose();
}
