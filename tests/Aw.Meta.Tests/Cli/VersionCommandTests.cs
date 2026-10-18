using System.Text.Json;

namespace Aw.Meta.Tests.Cli;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class VersionCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Version_json_names_the_commit_build_time_os_and_runtime_identifier()
    {
        var shown = await _cli.Run("version", "--json");

        Assert.Equal(0, shown.Exit);
        using var document = JsonDocument.Parse(shown.Out);
        Assert.All(["commit", "builtAt", "os", "rid"], name => Assert.False(string.IsNullOrEmpty(document.RootElement.GetProperty(name).GetString()), name));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Version_prints_one_line_and_needs_no_team()
    {
        var shown = await _cli.Run("version");

        Assert.Equal(0, shown.Exit);
        Assert.StartsWith("aw-meta commit ", shown.Out, StringComparison.Ordinal);
        _ = Assert.Single(shown.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Version_works_when_several_teams_exist_and_none_is_selected()
    {
        _ = await _cli.Run("team-init", "--team", "a", "--repo", "/r", "--lanes", "x");
        _ = await _cli.Run("team-init", "--team", "b", "--repo", "/r", "--lanes", "x");

        var shown = await _cli.Run("version");

        Assert.Equal(0, shown.Exit);
        Assert.StartsWith("aw-meta commit ", shown.Out, StringComparison.Ordinal);
    }

    public void Dispose() => _cli.Dispose();
}
