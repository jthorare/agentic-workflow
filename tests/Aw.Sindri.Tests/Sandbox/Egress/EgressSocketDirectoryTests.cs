using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

public sealed class EgressSocketDirectoryTests
{
    [Fact, Trait("Lane", "unit")]
    public void Socket_path_is_under_the_limit()
    {
        var directory = EgressSocketDirectory.PathFor(n => n == "XDG_RUNTIME_DIR" ? "/run/user/1000" : null, 1000, "a1b2c3d4");

        Assert.Equal("/run/user/1000/sindri/a1b2c3d4", directory);
        Assert.True(EgressSocketDirectory.SocketPathsFit(directory));
        Assert.False(EgressSocketDirectory.SocketPathsFit("/run/user/1000/" + new string('x', 90)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_runtime_directory_it_falls_back_to_tmp_by_uid() =>
        Assert.Equal("/tmp/sindri-1000/a1b2c3d4", EgressSocketDirectory.PathFor(_ => null, 1000, "a1b2c3d4"));

    [Fact, Trait("Lane", "integration")]
    public void Created_directory_is_owner_only()
    {
        using var temp = new TempDirectory();
        var directory = Path.Combine(temp.Path, "sindri", "a1b2c3d4");

        Assert.SkipWhen(OperatingSystem.IsWindows(), "unix modes");
        EgressSocketDirectory.Create(directory);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(directory));
    }
}
