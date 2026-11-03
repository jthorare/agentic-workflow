namespace Aw.Judge.Tests.SetConfig;

// Pins the field name ui-evidence's JudgeCliJudge.JevImagesOnAsync reads out of `judge config get`: providers.jevImages.
public sealed class ConfigGetJevImagesTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    public void Dispose() => _state.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task Config_get_after_jev_images_true_has_providers_jevImages_true()
    {
        var cli = new JudgeCli(_state);

        _ = await cli.Run("config", "set", "providers", "jev-images", "true");
        var get = await cli.Run("config", "get");

        Assert.Equal(0, get.ExitCode);
        Assert.True(get.Json().GetProperty("providers").GetProperty("jevImages").GetBoolean());
    }
}
