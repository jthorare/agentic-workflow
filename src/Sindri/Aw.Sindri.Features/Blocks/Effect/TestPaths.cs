namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>Whether a repo path is a test (PB spec §10.1: a push is 'pushedSource' only when a commit touches a path that is not).</summary>
internal static class TestPaths
{
    private static readonly string[] _directories = ["test", "tests", "__tests__", "spec", "specs"];
    private static readonly string[] _fileMarkers = [".test.", ".spec.", "Tests.", "Test.", "_test."];

    /// <summary>True when any segment of <paramref name="path"/> is a test directory (or a '.Tests' or '.Testing' project) or the file name marks a test.</summary>
    /// <param name="path">The path, relative to the repo.</param>
    /// <returns>Whether it is a test path.</returns>
    public static bool Is(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        var directories = segments[..^1];
        return directories.Any(static d => _directories.Contains(d, StringComparer.OrdinalIgnoreCase)
                || d.EndsWith(".Tests", StringComparison.Ordinal) || d.EndsWith(".Testing", StringComparison.Ordinal))
            || _fileMarkers.Any(m => segments[^1].Contains(m, StringComparison.Ordinal));
    }
}
