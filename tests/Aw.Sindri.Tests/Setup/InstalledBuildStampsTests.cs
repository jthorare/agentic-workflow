using Aw.Sindri.Infrastructure.Setup;

namespace Aw.Sindri.Tests.Setup;

/// <summary>The stamps of the install layout (#1032): a build runs from bin/cs/&lt;stamp&gt; and bin/cs/current links to the installed one.</summary>
public sealed class InstalledBuildStampsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("stamps-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Build(string stamp)
    {
        var folder = Path.Combine(_root, "bin", "cs", stamp);
        _ = Directory.CreateDirectory(folder);
        return folder + Path.DirectorySeparatorChar;
    }

    [Fact, Trait("Lane", "unit")]
    public void A_build_in_bin_cs_names_itself_and_the_link_target()
    {
        var older = Build("older");
        _ = Build("newer");
        _ = Directory.CreateSymbolicLink(Path.Combine(_root, "bin", "cs", "current"), Path.Combine(_root, "bin", "cs", "newer"));

        var stamps = new InstalledBuildStamps(older);

        Assert.Equal(("older", "newer"), (stamps.Running(), stamps.Installed()));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_build_without_a_current_link_has_no_installed_stamp()
    {
        var stamps = new InstalledBuildStamps(Build("only"));

        Assert.Equal(("only", null), (stamps.Running(), stamps.Installed()));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_process_outside_bin_cs_has_no_stamps()
    {
        var stamps = new InstalledBuildStamps(_root + Path.DirectorySeparatorChar);

        Assert.Equal((null, null), (stamps.Running(), stamps.Installed()));
    }
}
