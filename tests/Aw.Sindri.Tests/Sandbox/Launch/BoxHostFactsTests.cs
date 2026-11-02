using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Sandbox.Launch;

namespace Aw.Sindri.Tests.Sandbox.Launch;

[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class BoxHostFactsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-boxfacts-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void The_state_dir_is_AW_STATE_DIR_or_dot_agentic_workflow_under_home()
    {
        Assert.Equal("/s", BoxHostFacts.StateDir(static n => n == "AW_STATE_DIR" ? "/s" : null, "/home/o"));
        Assert.Equal("/home/o/.agentic-workflow", BoxHostFacts.StateDir(static _ => null, "/home/o"));
    }

    [Fact]
    public void Bin_cs_current_resolves_to_its_stamp_and_none_installed_is_empty()
    {
        Assert.Equal(string.Empty, BoxHostFacts.CsBinDir(_dir));
        var stamp = Directory.CreateDirectory(Path.Combine(_dir, "bin", "cs", "20261025T101500-ab12cd3"));
        _ = File.CreateSymbolicLink(Path.Combine(_dir, "bin", "cs", "current"), stamp.FullName);

        Assert.Equal(Path.Combine(_dir, "bin", "cs", "20261025T101500-ab12cd3"), BoxHostFacts.CsBinDir(_dir));
    }

    [Fact]
    public void Other_homes_are_the_siblings_of_the_owners_home()
    {
        _ = Directory.CreateDirectory(Path.Combine(_dir, "owner"));
        _ = Directory.CreateDirectory(Path.Combine(_dir, "other"));
        _ = Directory.CreateDirectory(Path.Combine(_dir, "another"));

        var others = BoxHostFacts.OtherHomes(_dir, Path.Combine(_dir, "owner"));

        Assert.Equal([Path.Combine(_dir, "another"), Path.Combine(_dir, "other")], others);
    }
}
