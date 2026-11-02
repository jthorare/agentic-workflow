namespace Aw.Context.Core.Extract;

/// <summary>Which files are user journeys (S-FLOW-J): ui-evidence plans and Playwright specs. The extractor reads them for flows only; no other pass sees them.</summary>
public static class Journeys
{
    /// <summary>The most journey files one build reads; the rest are skipped and counted.</summary>
    public const int MaxFiles = 500;

    private static readonly string[] _specFolders = ["e2e", "e2e-tests", "playwright"];
    private static readonly string[] _specExtensions = [".ts", ".js", ".tsx", ".jsx", ".mjs"];
    private static readonly string[] _built = ["node_modules", "dist", "build", "coverage", ".git"];

    /// <summary>Whether a path is a journey file.</summary>
    /// <param name="path">The path from the repo root, with '/' separators.</param>
    /// <returns>True for a '.ui-evidence/*.json' plan, or a '*.spec.*' or '*.e2e.*' script under an 'e2e', 'e2e-tests' or 'playwright' folder.</returns>
    public static bool IsJourney(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split('/');
        if (segments.Length < 2 || segments.SkipLast(1).Any(s => _built.Contains(s, StringComparer.Ordinal)))
        {
            return false;
        }

        var name = segments[^1];
        var folders = segments.SkipLast(1).ToList();
        return (folders.Contains(".ui-evidence", StringComparer.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            || (folders.Exists(f => _specFolders.Contains(f, StringComparer.Ordinal))
                && _specExtensions.Any(e => name.EndsWith(e, StringComparison.Ordinal))
                && (name.Contains(".spec.", StringComparison.Ordinal) || name.Contains(".e2e.", StringComparison.Ordinal)));
    }

    /// <summary>Keeps at most <see cref="MaxFiles"/> journey paths (the first in ordinal order); every other path passes.</summary>
    /// <param name="paths">The paths a loader means to read.</param>
    /// <returns>The paths to read.</returns>
    public static IReadOnlyList<string> Cap(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var all = paths.ToList();
        var keep = all.Where(IsJourney).Order(StringComparer.Ordinal).Take(MaxFiles).ToHashSet(StringComparer.Ordinal);
        return [.. all.Where(p => !IsJourney(p) || keep.Contains(p))];
    }
}
