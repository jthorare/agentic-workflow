namespace Aw.Meta.Tests.Cli;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class RouteCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private async Task Ready(string routing = "")
    {
        var path = _cli.File("team.json");
        await File.WriteAllTextAsync(path, $$"""{"team":"t1","repo":"/r","lanes":["meta","api"]{{routing}}}""", TestContext.Current.CancellationToken);
        _cli.SetEnv("AW_META_CONFIG", path);
    }

    private const string Pairs = ""","routing":{"models":{"claude-code":["opus"],"codex":["gpt-5.5"]},"equivalents":{"opus":"gpt-5.5"},"defaultModel":"opus"}""";

    [Fact, Trait("Lane", "integration")]
    public async Task Route_with_nothing_to_move_says_so_and_exits_0()
    {
        await Ready();

        var result = await _cli.Run("route");

        Assert.Equal(0, result.Exit);
        Assert.Contains("nothing to switch", result.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Route_json_has_the_suggestions_the_skipped_lanes_and_the_flags()
    {
        await Ready(Pairs);

        var result = await _cli.Run("route", "--json");

        Assert.Equal(0, result.Exit);
        var compact = result.Out.Replace(" ", string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"suggestions\":[]", compact, StringComparison.Ordinal);
        Assert.Contains("\"applied\":false", compact, StringComparison.Ordinal);
        Assert.Contains("\"attention\":false", compact, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Route_refuses_a_lane_the_team_does_not_have_as_a_usage_error()
    {
        await Ready(Pairs);

        var result = await _cli.Run("route", "--lane", "nope");

        Assert.Equal(2, result.Exit);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Usage_prints_the_agent_and_model_of_each_lane_and_says_when_a_provider_shows_no_figure()
    {
        await Ready(Pairs);

        var result = await _cli.Run("usage");

        Assert.Contains("claude-code/opus", result.Out, StringComparison.Ordinal);
        Assert.Contains("Claude Code shows only the limit stop", result.Out, StringComparison.Ordinal);
    }

    public void Dispose() => _cli.Dispose();
}
