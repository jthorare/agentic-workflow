namespace Aw.Kernel.Data.Tests;

[Trait("Lane", "integration")]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ExecutableFilesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-exec-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string WriteFile(string name, UnixFileMode mode)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "data");
        File.SetUnixFileMode(path, mode);
        return path;
    }

    private const UnixFileMode Rwx = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    [Fact]
    public void A_file_with_an_execute_bit_is_executable() => Assert.True(ExecutableFiles.IsExecutable(WriteFile("tool", Rwx)));

    [Fact]
    public void A_regular_file_without_an_execute_bit_is_not() => Assert.False(ExecutableFiles.IsExecutable(WriteFile("data", UnixFileMode.UserRead | UnixFileMode.UserWrite)));

    [Fact]
    public void A_missing_path_is_not() => Assert.False(ExecutableFiles.IsExecutable(Path.Combine(_dir, "nope")));

    [Fact]
    public void A_dangling_link_is_not_and_does_not_throw()
    {
        var link = Path.Combine(_dir, "dangling");
        _ = File.CreateSymbolicLink(link, Path.Combine(_dir, "gone"));

        Assert.False(ExecutableFiles.IsExecutable(link));
        Assert.False(ExecutableFiles.IsExecutableByOthers(link));
    }

    [Fact]
    public void A_link_to_an_executable_is()
    {
        var link = Path.Combine(_dir, "alias");
        _ = File.CreateSymbolicLink(link, WriteFile("real", Rwx));

        Assert.True(ExecutableFiles.IsExecutable(link));
    }

    [Fact]
    public void The_others_check_wants_the_other_execute_bit()
    {
        Assert.False(ExecutableFiles.IsExecutableByOthers(WriteFile("user-only", Rwx)));
        Assert.True(ExecutableFiles.IsExecutableByOthers(WriteFile("all", Rwx | UnixFileMode.OtherExecute)));
    }
}
