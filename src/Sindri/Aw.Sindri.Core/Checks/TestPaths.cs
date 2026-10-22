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
}
