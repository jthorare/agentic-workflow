namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>Whether an image sits under one of the places a 'step.judge.choose' image may come from, with symlinks followed on both sides.</summary>
internal static class StepImagePlaces
{
    /// <summary>Whether the file's real path is under one of the directories' real paths.</summary>
    /// <param name="path">The image's absolute path.</param>
    /// <param name="roots">The allowed directories.</param>
    /// <returns>True when it is inside one.</returns>
    public static bool Inside(string path, IReadOnlyList<string> roots)
    {
        var real = Real(new FileInfo(path));
        return roots.Any(root => Under(real, Real(new DirectoryInfo(root))));
    }

    private static bool Under(string real, string root)
    {
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return real.StartsWith(prefix, StringComparison.Ordinal);
    }

    // The final target of a link (the file's or the directory's own), else the full path; the parts above a link are the caller's own.
    private static string Real(FileSystemInfo info)
    {
        try
        {
            return (info.ResolveLinkTarget(returnFinalTarget: true) ?? info).FullName;
        }
        catch (IOException)
        {
            return info.FullName;   // boundary: an unreadable link counts as itself, which the existence check has already vouched for
        }
    }
}
