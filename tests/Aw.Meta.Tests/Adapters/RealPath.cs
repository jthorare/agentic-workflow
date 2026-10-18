namespace Aw.Meta.Tests.Adapters;

/// <summary>The physical form of a path: every symlink along it resolved (macOS's temp directory is under /var, a link to /private/var).</summary>
public static class RealPath
{
    public static string Of(string path)
    {
        var current = Path.GetPathRoot(path) ?? "/";
        foreach (var segment in path[current.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) && new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: true) is { } target)
            {
                current = target.FullName;
            }
        }

        return current;
    }
}
