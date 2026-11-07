namespace Aw.Kernel.Data.Tests;

[Trait("Lane", "integration")]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class FileModesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-modes-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private const UnixFileMode Rw = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead;

    [Fact]
    public void A_file_answers_its_mode()
    {
        var path = Path.Combine(_dir, "data");
        File.WriteAllText(path, "data");
        File.SetUnixFileMode(path, Rw);

        Assert.Equal(Rw, FileModes.TryRead(path));
    }

    [Fact]
    public void A_directory_answers_its_mode()
    {
        var path = Path.Combine(_dir, "sub");
        _ = Directory.CreateDirectory(path);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, FileModes.TryRead(path));
    }

    [Fact]
    public void A_missing_path_is_unknown() => Assert.Null(FileModes.TryRead(Path.Combine(_dir, "nope")));

    [Fact]
    public void A_dangling_link_is_unknown_and_does_not_throw()
    {
        var link = Path.Combine(_dir, "dangling");
        _ = File.CreateSymbolicLink(link, Path.Combine(_dir, "gone"));

        Assert.Null(FileModes.TryRead(link));
    }
}
