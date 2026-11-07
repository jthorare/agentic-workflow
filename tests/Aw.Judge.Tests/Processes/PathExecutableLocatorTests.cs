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
    public void A_dangling_link_on_the_path_is_not_found()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Symbolic links need a privilege on Windows");
        _ = File.CreateSymbolicLink(Path.Combine(_path.First, "cursor-agent"), Path.Combine(_path.First, "missing-target"));

        Assert.False(new PathExecutableLocator(name => name == "PATH" ? _path.Path : null).IsOnPath("cursor-agent"));
    }

    [Fact]
    public void A_dangling_link_does_not_hide_a_real_binary_later_on_the_path()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Symbolic links need a privilege on Windows");
        _ = File.CreateSymbolicLink(Path.Combine(_path.First, "codex"), Path.Combine(_path.First, "missing-target"));
        PathDirectories.AddFile(_path.Second, "codex", executable: true);

        Assert.True(new PathExecutableLocator(name => name == "PATH" ? _path.Path : null).IsOnPath("codex"));
    }

    [Fact]
    public void Nothing_is_found_without_a_path()
    {
        PathDirectories.AddFile(_path.First, "codex", executable: true);

        Assert.False(new PathExecutableLocator(_ => null).IsOnPath("codex"));
    }
}
