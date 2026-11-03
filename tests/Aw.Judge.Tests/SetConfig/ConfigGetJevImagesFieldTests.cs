using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.SetConfig;

/// <summary>
/// Pins the field name 'judge config get' prints for the Jev image switch. ui-evidence's JudgeCliJudge.JevImagesOnAsync reads
/// providers.jevImages from this output to key its verdict cache, so a rename here silently turns its cache key off.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ConfigGetJevImagesFieldTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    public void Dispose() => _state.Dispose();

    // Green on the red commit: the field already has this name; the test keeps it from moving.
    [Fact]
    public async Task ConfigGet_prints_providers_jevImages_true_after_jev_images_true()
    {
        var cli = new JudgeCli(_state);

        var set = await cli.Run("config", "set", "providers", "jev-images", "true");
        var get = await cli.Run("config", "get");

        Assert.Equal(0, set.ExitCode);
        Assert.Equal((0, ""), (get.ExitCode, get.Stderr));
        var providers = get.Json().GetProperty("providers");
        Assert.Equal(System.Text.Json.JsonValueKind.True, providers.GetProperty("jevImages").ValueKind);
    }
}
