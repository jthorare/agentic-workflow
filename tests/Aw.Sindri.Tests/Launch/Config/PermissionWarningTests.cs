using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Launch.Config;

/// <summary>The file may run commands as the user, like a shell rc (launcher spec, permissions): doctor warns when it or its directory is open to others.</summary>
public sealed class PermissionWarningTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-perm").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private string File0(UnixFileMode file, UnixFileMode directory)
    {
        var path = Path.Combine(_home, "launcher.json");
        File.WriteAllText(path, "{}");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, file);
            File.SetUnixFileMode(_home, directory);
        }

        return path;
    }

    private const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PrivateDirectory = Private | UnixFileMode.UserExecute;

    [Fact, Trait("Lane", "unit")]
    public void A_private_file_in_a_private_directory_warns_of_nothing() =>
        Assert.Empty(LauncherFilePermissions.Warnings(File0(Private, PrivateDirectory)));

    [Fact, Trait("Lane", "unit")]
    public void Group_writable_file_warns_in_doctor()
    {
        var warnings = LauncherFilePermissions.Warnings(File0(Private | UnixFileMode.GroupWrite, PrivateDirectory));

        Assert.Equal("launcher.json is group- or world-writable (fix: chmod go-w)", Assert.Single(warnings).Replace(_home + "/", string.Empty, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void World_writable_file_and_a_world_writable_parent_each_warn()
    {
        var warnings = LauncherFilePermissions.Warnings(File0(Private | UnixFileMode.OtherWrite, PrivateDirectory | UnixFileMode.OtherWrite));

        Assert.Equal(2, warnings.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_missing_file_warns_of_nothing() =>
        Assert.Empty(LauncherFilePermissions.Warnings(Path.Combine(_home, "missing.json")));
}
