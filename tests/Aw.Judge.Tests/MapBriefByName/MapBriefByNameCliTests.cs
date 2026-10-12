namespace Aw.Judge.Tests.MapBriefByName;

// The frozen surface of 'judge brief map-by-name' (TS runBriefMapByName: the tool use id or nothing, exit 0).
public sealed class MapBriefByNameCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Map_by_name_prints_the_oldest_unmapped_brief_under_the_name()
    {
        var cli = new JudgeCli(_state);
        await Save(cli, "toolu_a", "helper");
        cli.Clock.Advance(TimeSpan.FromSeconds(1));
        await Save(cli, "toolu_b", "helper");

        Assert.Equal(new CliRun(0, "toolu_a\n", ""), await cli.Run("brief", "map-by-name", "helper"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Map_by_name_prints_nothing_when_no_brief_has_the_name() =>
        Assert.Equal(new CliRun(0, "", ""), await new JudgeCli(_state).Run("brief", "map-by-name", "nobody"));

    private static async Task Save(JudgeCli cli, string toolUseId, string name) =>
        Assert.Equal(0, (await cli.RunWithInput(
            $$"""{"toolUseId":"{{toolUseId}}","sessionId":"s","promptId":"p","dispatchName":"{{name}}","subagentType":"x","goal":"g","acceptanceCriteria":"a","proofCommand":"c"}""",
            "brief", "save")).ExitCode);

    public void Dispose() => _state.Dispose();
}
