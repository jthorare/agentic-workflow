namespace Aw.Meta.Tests.Cli;

/// <summary>The watch command's flags: --until-mail and --until-event end the watch in different ways, so both at once is a usage error.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class WatchFlagsTests : IDisposable
{
    private readonly CliHarness _cli = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Until_mail_with_until_event_is_a_usage_error()
    {
        await File.WriteAllTextAsync(_cli.File("team.json"), """{"team":"t","repo":"/nonexistent","lanes":["api"],"metaLane":"meta"}""", TestContext.Current.CancellationToken);
        _cli.SetEnv("AW_META_CONFIG", _cli.File("team.json"));

        var result = await _cli.Run("watch", "--until-mail", "--until-event");

        Assert.Equal(2, result.Exit);
        Assert.Contains("--until-mail and --until-event", result.Err, StringComparison.Ordinal);
        Assert.Empty(result.Out);
    }

    public void Dispose() => _cli.Dispose();
}
