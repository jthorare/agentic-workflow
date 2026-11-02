using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Setup;

/// <summary>
/// The stamps of scripts/install-cs.sh's layout: a build runs from '$AW_STATE_DIR/bin/cs/&lt;stamp&gt;/' and 'bin/cs/current' is the link to the installed one. A process that does not run from a
/// folder inside 'bin/cs' (dotnet run, a test) has no stamp, and then neither stamp is known.
/// </summary>
/// <param name="baseDirectory">The folder the process runs from (AppContext.BaseDirectory).</param>
internal sealed class InstalledBuildStamps(string baseDirectory) : IBuildStamps
{
    private const string Layout = "cs";

    public string? Running() => Stamp() is { } stamp ? stamp.Name : null;

    public string? Installed()
    {
        if (Stamp() is not { } stamp)
        {
            return null;
        }

        var link = Path.Combine(stamp.Parent, "current");
        if (!Path.Exists(link))
        {
            return null;   // why: ResolveLinkTarget throws on a path that isn't there; a missing or dangling link names no build
        }

        var target = Directory.ResolveLinkTarget(link, returnFinalTarget: false);
        return target is null ? null : Path.GetFileName(target.FullName.TrimEnd(Path.DirectorySeparatorChar));
    }

    private (string Name, string Parent)? Stamp()
    {
        var folder = baseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var parent = Path.GetDirectoryName(folder);
        return parent is not null && Path.GetFileName(parent) == Layout && Path.GetFileName(Path.GetDirectoryName(parent)) == "bin"
            ? (Path.GetFileName(folder), parent)
            : null;
    }
}
