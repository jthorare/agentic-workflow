using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Commands;

/// <summary>The app-path check on a dangling symlink (#2217): File.Exists is true for it, File.GetUnixFileMode follows the link and throws.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class UiLauncherDanglingLinkTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-ui-dangling-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact, Trait("Lane", "unit")]
    public void A_dangling_link_as_the_app_answers_not_found_or_not_executable_and_does_not_throw()
    {
        var link = Path.Combine(_dir, "sindri-ui");
        _ = File.CreateSymbolicLink(link, Path.Combine(_dir, "no-such-target"));

        var line = UiLauncher.Open(link);

        Assert.StartsWith("not ", line, StringComparison.Ordinal);
        Assert.Contains(link, line, StringComparison.Ordinal);
    }
}
