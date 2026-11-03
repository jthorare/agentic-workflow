namespace Aw.Judge.Tests.SetConfig;

// 'judge config set providers jev-images true|false': the one pasted command that puts every image call back on the agent CLIs.
public sealed class SetConfigJevImagesTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    private string ConfigFile => Path.Combine(_state.Path, "judge", "config.json");

    public void Dispose() => _state.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task ConfigSet_providers_jev_images_saves_and_prints()
    {
        var cli = new JudgeCli(_state);

        var off = await cli.Run("config", "set", "providers", "jev-images", "false");

        Assert.Equal((0, ""), (off.ExitCode, off.Stderr));
        Assert.False(off.Json().GetProperty("providers").GetProperty("jevImages").GetBoolean());
        Assert.False((await cli.Run("config", "get")).Json().GetProperty("providers").GetProperty("jevImages").GetBoolean());

        var on = await cli.Run("config", "set", "providers", "jev-images", "true");

        Assert.Equal((0, ""), (on.ExitCode, on.Stderr));
        Assert.True(on.Json().GetProperty("providers").GetProperty("jevImages").GetBoolean());
        Assert.True((await cli.Run("config", "get")).Json().GetProperty("providers").GetProperty("jevImages").GetBoolean());

        var other = await cli.Run("config", "set", "providers", "jev-images", "maybe");

        Assert.Equal((64, ""), (other.ExitCode, other.Stdout));
        Assert.StartsWith("usage: judge config set providers jev-images true|false", other.Stderr, StringComparison.Ordinal);
        Assert.True((await cli.Run("config", "get")).Json().GetProperty("providers").GetProperty("jevImages").GetBoolean());
        Assert.True(File.Exists(ConfigFile));
    }
}
