using Aw.Kernel.Testing;
using Aw.Sindri.Host.Commands.SessionWrap;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>The missing-CLI guard on a dangling symlink (#2217): File.Exists is true for it, File.GetUnixFileMode follows the link and throws, so the guard must answer false instead.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class BinOnPathDanglingLinkTests : IDisposable
{
    private readonly string _dangling = Directory.CreateTempSubdirectory("aw-wrap-dangling-").FullName;
    private readonly string _real = Directory.CreateTempSubdirectory("aw-wrap-real-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dangling, recursive: true);
        Directory.Delete(_real, recursive: true);
    }

    private string DanglingLink()
    {
        var link = Path.Combine(_dangling, "fake-agent");
        _ = File.CreateSymbolicLink(link, Path.Combine(_dangling, "no-such-target"));
        return link;
    }

    [Fact]
    public void A_dangling_link_earlier_on_the_path_is_skipped_for_a_real_executable_later_on_it()
    {
        _ = DanglingLink();
        _ = ExecutableScript.Write(Path.Combine(_real, "fake-agent"), "exit 0\n");

        Assert.True(BinOnPath.Resolves(bin: "fake-agent", path: _dangling + ":" + _real));
    }

    [Fact]
    public void A_dangling_link_named_by_path_does_not_resolve() =>
        Assert.False(BinOnPath.Resolves(bin: DanglingLink(), path: null));
}
