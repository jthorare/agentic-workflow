namespace Aw.Sindri.Core.Checks;

/// <summary>The test-path convention the scope gate and the test rule share (line spec §5.1, FA §17.23.3): a file under a test directory, or named like a test.</summary>
public static class TestPaths
{
    private static readonly string[] _directories = ["tests", "test", "__tests__", "spec", "specs"];
    private static readonly string[] _suffixes = [".test", ".spec", "_test", "_spec", "Tests", "Test", "Spec"];

    /// <summary>True when <paramref name="path"/> is a test file by convention.</summary>
    /// <param name="path">A repo-relative path with '/' separators.</param>
    /// <returns>Whether it is a test file.</returns>
    public static bool IsTest(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split('/');
        if (segments[..^1].Any(static s => _directories.Contains(s, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        var file = segments[^1];
        var dot = file.LastIndexOf('.');
        var name = dot > 0 ? file[..dot] : file;
        return name.StartsWith("test_", StringComparison.OrdinalIgnoreCase) || _suffixes.Any(s => name.EndsWith(s, StringComparison.Ordinal) && name.Length > s.Length);
    }

    /// <summary>The existing test files a phase allows without naming them: PLN-TEST-001's one rule, shared by the line's plan checks and the plan station's.</summary>
    /// <remarks>BLD-TEST-001 fails a build that changes an existing test its phase doesn't name. An entry the phase lists is taken as existing; 'new: path' entries and globs are not (the plan can't know what a glob matches).</remarks>
    /// <param name="allowed">The phase's write scope, or its allowed files.</param>
    /// <param name="named">The paths of the phase's test-change entries.</param>
    /// <returns>The trimmed test paths that are allowed and not named, in order.</returns>
    public static IEnumerable<string> UnnamedExisting(IEnumerable<string> allowed, IReadOnlyCollection<string> named)
    {
        ArgumentNullException.ThrowIfNull(allowed);
        ArgumentNullException.ThrowIfNull(named);
        return allowed.Select(static e => e.Trim())
            .Where(e => !e.StartsWith("new:", StringComparison.Ordinal) && e.IndexOfAny(['*', '?']) < 0 && IsTest(e) && !named.Contains(e, StringComparer.Ordinal));
    }

    /// <summary>The entries a phase allows: its write scope, or its allowed files when the write scope is empty.</summary>
    /// <param name="writeScope">The phase's 'writeScope' entries.</param>
    /// <param name="allowedFiles">The phase's 'allowedFiles' entries.</param>
    /// <returns>The entries PLN-TEST-001 looks at.</returns>
    public static IReadOnlyList<string> AllowedEntries(IReadOnlyList<string> writeScope, IReadOnlyList<string> allowedFiles)
    {
        ArgumentNullException.ThrowIfNull(writeScope);
        ArgumentNullException.ThrowIfNull(allowedFiles);
        return writeScope.Count > 0 ? writeScope : allowedFiles;
    }
}
