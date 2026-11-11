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
    public void The_profile_path_is_named_only_when_its_folder_exists()
    {
        var file = Path.Combine(_dir, "profile", "profile.yaml");
        Assert.Null(BoxHostFacts.ProfilePath(static _ => null, _dir));   // no profile/ folder: no bind, no AW_SINDRI_PROFILE (bwrap fails on a missing bind source)

        _ = Directory.CreateDirectory(Path.Combine(_dir, "profile"));
        Assert.Equal(file, BoxHostFacts.ProfilePath(static _ => null, _dir));
        Assert.Equal(file, BoxHostFacts.ProfilePath(n => n == "AW_SINDRI_PROFILE" ? file : null, "/nowhere"));
        Assert.Null(BoxHostFacts.ProfilePath(static n => n == "AW_SINDRI_PROFILE" ? "/nowhere/live.yaml" : null, _dir));
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

    // #2162: a PATH directory with a claude entry of the given kind; returns the directory.
    private string PathDir(string name, Action<string> make)
    {
        var dir = Directory.CreateDirectory(Path.Combine(_dir, "path", name)).FullName;
        make(Path.Combine(dir, "claude"));
        return dir;
    }

    private string Binary(string name)
    {
        var file = Path.Combine(Directory.CreateDirectory(Path.Combine(_dir, "share", name)).FullName, "claude-bin");
        File.WriteAllText(file, "#!/bin/sh\n");
        return file;
    }

    [Fact]
    public void A_symlinked_agent_binary_on_path_maps_to_its_real_file()
    {
        var real = Binary("a");
        var dir = PathDir("bin", link => File.CreateSymbolicLink(link, real));

        var links = BoxHostFacts.AgentBinaryLinks(dir);

        var only = Assert.Single(links);
        Assert.Equal(Path.Combine(dir, "claude"), only.Key);
        Assert.Equal(real, only.Value);
    }

    [Fact]
    public void A_plain_file_agent_binary_on_path_is_not_a_link()
    {
        var dir = PathDir("bin", file => File.WriteAllText(file, "#!/bin/sh\n"));

        Assert.Empty(BoxHostFacts.AgentBinaryLinks(dir));
    }

    [Fact]
    public void A_dangling_agent_binary_link_on_path_is_skipped()
    {
        var dir = PathDir("bin", link => File.CreateSymbolicLink(link, Path.Combine(_dir, "gone")));

        Assert.Empty(BoxHostFacts.AgentBinaryLinks(dir));
    }

    [Fact]
    public void An_agent_binary_in_two_path_directories_counts_only_at_the_first_hit()
    {
        var first = PathDir("first", link => File.CreateSymbolicLink(link, Binary("one")));
        var second = PathDir("second", link => File.CreateSymbolicLink(link, Binary("two")));

        var links = BoxHostFacts.AgentBinaryLinks(first + ":" + second);

        Assert.Equal(Path.Combine(first, "claude"), Assert.Single(links).Key);
    }
}
