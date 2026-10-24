using System.Text.RegularExpressions;

namespace Aw.BugFix.Core.Rules;

/// <summary>
/// Files a fix must not touch without an approved reason (--allow-test-changes): the check and its directory (unless
/// that is the repository root), tests, fixtures, mocks, snapshots and test-runner config. Weakening what the check
/// exercises is as bad as editing the check. Build and lock files (package.json, Makefile, go.mod, Directory.Build.*,
/// tsconfig and the like) are ordinary changes and need no flag: the frozen command and its environment digest still
/// decide what the check runs.
/// </summary>
public static partial class ProtectedPaths
{
    /// <summary>The changed files a fix may not touch without approval.</summary>
    /// <param name="changedFiles">Repository-relative paths with '/'.</param>
    /// <param name="checkPath">The frozen check's repository-relative path.</param>
    /// <param name="commandFiles">Repository files the frozen command names (its runner, wrapper scripts).</param>
    /// <returns>The protected ones, in input order.</returns>
    public static IReadOnlyList<string> Touched(IEnumerable<string> changedFiles, string checkPath, IReadOnlyCollection<string> commandFiles)
    {
        var slash = checkPath.LastIndexOf('/');
        var checkDirectory = slash < 0 ? null : checkPath[..(slash + 1)];
        return [.. changedFiles.Where(f =>
            f == checkPath
            || commandFiles.Contains(f, StringComparer.Ordinal)
            || (checkDirectory is not null && f.StartsWith(checkDirectory, StringComparison.Ordinal))
            || Matches(f))];
    }

    /// <summary>
    /// The added files that a loader could pick ahead of existing code, so the check runs something else with no
    /// protected file changed: a module beside the check or in the run's directory (both on Python's sys.path first, so
    /// a new argparse.py or unittest/__init__.py shadows the standard library), compiled or native code (.pyc, .so,
    /// .node, .pth), interpreter start-up hooks, and anything under node_modules.
    /// </summary>
    /// <param name="addedFiles">Repository-relative paths added since the baseline.</param>
    /// <param name="runDirectory">The frozen run's directory, relative to the repository root ("" for the root).</param>
    /// <param name="checkPath">The frozen check's repository-relative path.</param>
    /// <returns>The shadowing ones, in input order.</returns>
    public static IReadOnlyList<string> Shadowing(IEnumerable<string> addedFiles, string runDirectory, string checkPath)
    {
        var slash = checkPath.LastIndexOf('/');
        string[] importRoots = [runDirectory.Length == 0 ? string.Empty : runDirectory + "/", slash < 0 ? string.Empty : checkPath[..(slash + 1)]];
        return [.. addedFiles.Where(f => LoadsFirst(f) || importRoots.Any(root => TopModule(f, root)))];
    }

    private static bool LoadsFirst(string file)
    {
        try
        {
            return LoadedFirst.IsMatch(file);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;   // a timeout never lets a change through
        }
    }

    // root/x.py or root/x/__init__.py: importable by name from that directory.
    private static bool TopModule(string file, string root)
    {
        if (!file.StartsWith(root, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = file[root.Length..];
        var parts = rest.Split('/');
        return (parts.Length == 1 && rest.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            || (parts.Length == 2 && string.Equals(parts[1], "__init__.py", StringComparison.OrdinalIgnoreCase));
    }

    // A pattern that times out counts as protected: a timeout never lets a change through.
    private static bool Matches(string file)
    {
        try
        {
            return TestDirectoryOrName.IsMatch(file) || RunnerConfig.IsMatch(file) || JvmStyleTest.IsMatch(file);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }

    [GeneratedRegex(
        "(^|/)(tests?|__tests__|specs?|fixtures?|__fixtures__|mocks?|__mocks__|__snapshots__|e2e|cypress|playwright|test-?utils|testing|testdata|\\.ui-evidence)(/|$)"
        + "|\\.(test|spec)\\.[^/]+$|(^|/)test_[^/]+\\.py$|_test\\.(go|py|rb)$|\\.snap$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex TestDirectoryOrName { get; }

    [GeneratedRegex(
        "(^|/)(vitest|vite|jest|playwright|karma|cypress|babel)\\.(config|setup|workspace)\\.[^/]+$|(^|/)(jest|vitest)[.-]setup[^/]*$"
        + "|(^|/)setupTests\\.[^/]+$|(^|/)conftest\\.py$|(^|/)\\.mocharc[^/]*$|(^|/)(pytest\\.ini|tox\\.ini|pyproject\\.toml|setup\\.cfg)$"
        + "|(^|/)\\.babelrc[^/]*$|(^|/)\\.env\\.test[^/]*$|(^|/)noxfile\\.py$|(^|/)\\.pnpmfile\\.c?js$|\\.runsettings$"
        + "|(^|/)\\.?pytest\\.(ini|toml)$|(^|/)\\.rspec(-local)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex RunnerConfig { get; }

    [GeneratedRegex(
        "\\.(pyc|pyo|pyd|so|dylib|pth|node)$|(^|/)(sitecustomize|usercustomize)\\.py$|(^|/)node_modules/",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex LoadedFirst { get; }

    // FooTest.swift, AppUITests.swift, ApiTest.java: case-sensitive, so Latest.kt or Contest.cs don't match.
    [GeneratedRegex("(^|/)[^/]*Tests?\\.(swift|kt|java|cs)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex JvmStyleTest { get; }
}
