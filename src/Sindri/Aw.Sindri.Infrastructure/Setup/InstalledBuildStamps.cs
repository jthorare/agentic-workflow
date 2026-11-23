using System.Globalization;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Setup;

/// <summary>
/// The stamps of scripts/install-cs.sh's layout: a build runs from '$AW_STATE_DIR/bin/cs/&lt;stamp&gt;/' and 'bin/cs/current' is the link to the installed one. A process that does not run from a
/// folder inside 'bin/cs' (dotnet run, a test) has no stamp, and then neither stamp is known. Each build folder holds a 'build-info' ('commit=' and 'built=' lines) that the
/// install writes last; a folder without one is unfinished, or gone because the install pruned it.
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

    public BuildInfo? RunningBuild() => Stamp() is { } stamp ? Read(Path.Combine(stamp.Parent, stamp.Name, "build-info")) : null;

    public BuildInfo? InstalledBuild() => Stamp() is { } stamp && Installed() is { } installed ? Read(Path.Combine(stamp.Parent, installed, "build-info")) : null;

    /// <summary>Reads a 'build-info' file: a 'commit=' line and a 'built=' line (UTC, ISO 8601).</summary>
    /// <param name="path">The file.</param>
    /// <returns>The build, or null when the file is missing or lacks either line.</returns>
    internal static BuildInfo? Read(string path)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)   // boundary: a pruned or unreadable folder names no build
        {
            return null;
        }

        var commit = Value(lines, "commit=");
        var built = Value(lines, "built=");
        return commit is { Length: > 0 } && DateTimeOffset.TryParse(built, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when)
            ? new BuildInfo(commit, when)
            : null;
    }

    private static string? Value(string[] lines, string key) =>
        lines.FirstOrDefault(l => l.StartsWith(key, StringComparison.Ordinal)) is { } line ? line[key.Length..].Trim() : null;

    private (string Name, string Parent)? Stamp()
    {
        var folder = baseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var parent = Path.GetDirectoryName(folder);
        return parent is not null && Path.GetFileName(parent) == Layout && Path.GetFileName(Path.GetDirectoryName(parent)) == "bin"
            ? (Path.GetFileName(folder), parent)
            : null;
    }
}
