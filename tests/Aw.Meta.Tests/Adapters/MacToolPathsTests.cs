using Aw.Meta.Infrastructure.Files;

namespace Aw.Meta.Tests.Adapters;

public sealed class MacToolPathsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Each_tool_is_the_absolute_path_macOS_ships_it_at_and_never_a_bare_name()
    {
        var paths = MacToolPaths.Default;

        Assert.Equal(["/usr/sbin/sysctl", "/usr/bin/vm_stat", "/bin/ps", "/bin/kill", "/usr/sbin/lsof"], [paths.Sysctl, paths.VmStat, paths.Ps, paths.Kill, paths.Lsof]);
    }
}
