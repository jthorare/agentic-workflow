using Aw.Judge.Infrastructure.Processes;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Processes;

[Lane(TestLane.Integration)]
public sealed class PathExecutableLocatorTests : IDisposable
{
    private readonly PathDirectories _path = new();

    public void Dispose() => _path.Dispose();

    [Fact]
    public void A_file_without_an_execute_bit_is_not_found()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows has no execute bit");
        PathDirectories.AddFile(_path.First, "plain", executable: false);

        Assert.False(new PathExecutableLocator(name => name == "PATH" ? _path.Path : null).IsOnPath("plain"));
    }

    [Fact]
    public void A_directory_is_not_found()
    {
        Directory.CreateDirectory(Path.Combine(_path.Second, "adir"));

        Assert.False(new PathExecutableLocator(name => name == "PATH" ? _path.Path : null).IsOnPath("adir"));
    }

    [Fact]
    public void Nothing_is_found_without_a_path()
    {
        PathDirectories.AddFile(_path.First, "codex", executable: true);

        Assert.False(new PathExecutableLocator(_ => null).IsOnPath("codex"));
    }
}
