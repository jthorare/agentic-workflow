using System.Globalization;
using System.Reflection;

namespace Aw.Kernel.Testing;

/// <summary>
/// Golden-file (snapshot) checks for CLI output and JSON contracts (spec §4b.5). A golden file holds normalized output and
/// lives in the test project's source tree. Set 'AW_GOLDEN_UPDATE=1' locally to rewrite it from the current output:
/// the run then fails for each file it created or changed, so the diff gets reviewed; a file that already matches passes. Update mode is refused in CI, and writes never leave the source root.
/// </summary>
public static class Golden
{
    /// <summary>The environment variable that switches to update mode when it is '1'.</summary>
    public const string UpdateVariable = "AW_GOLDEN_UPDATE";

    /// <summary>
    /// The assembly-metadata key that holds a test project's source directory. Declare it in the test project:
    /// '&lt;AssemblyMetadata Include="AwGoldenSourceRoot" Value="$(MSBuildProjectDirectory)" /&gt;'. It embeds an
    /// absolute build-machine path, so only test assemblies declare it, never a shipped one.
    /// </summary>
    public const string SourceRootKey = "AwGoldenSourceRoot";

    /// <summary>
    /// The path of a golden file in the test project's source tree, so update mode rewrites the committed file
    /// rather than a copy in the build output.
    /// </summary>
    /// <param name="testAssembly">The test assembly; it declares <see cref="SourceRootKey"/>.</param>
    /// <param name="relativePath">The golden file, relative to the test project directory. No '..', not rooted.</param>
    /// <returns>The absolute path.</returns>
    /// <exception cref="InvalidOperationException">The assembly doesn't declare its source root.</exception>
    /// <exception cref="ArgumentException">The path is rooted or leaves the source root.</exception>
    public static string SourcePath(Assembly testAssembly, string relativePath) =>
        Resolve(SourceRootOf(testAssembly), relativePath);

    /// <summary>Checks output against a golden file in the test project's source tree (see <see cref="SourcePath"/>).</summary>
    /// <param name="actual">The output.</param>
    /// <param name="testAssembly">The test assembly; it declares <see cref="SourceRootKey"/>.</param>
    /// <param name="relativePath">The golden file, relative to the test project directory.</param>
    /// <param name="normalization">The declared normalization, applied to the output only: the golden file must
    /// already hold normalized text.</param>
    /// <exception cref="GoldenMismatchException">The output differs, the file is missing, update mode just rewrote
    /// it (review the diff), or update mode was asked for in CI.</exception>
    public static void Match(string actual, Assembly testAssembly, string relativePath, Normalization normalization) =>
        Match(
            actual,
            SourceRootOf(testAssembly),
            relativePath,
            normalization,
            update: string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "1", StringComparison.Ordinal),
            inCi: IsCi(Environment.GetEnvironmentVariable));

    /// <summary>
    /// Checks output against a golden file under an explicit root, for files outside the test project's own tree (a committed
    /// schema under the repo's schemas/ folder). Same rules as the assembly overload; the caller supplies update mode and CI.
    /// </summary>
    /// <param name="actual">The output.</param>
    /// <param name="sourceRoot">The root directory the golden path is relative to.</param>
    /// <param name="relativePath">The golden file, relative to <paramref name="sourceRoot"/>. No '..', not rooted.</param>
    /// <param name="normalization">The declared normalization, applied to the output only.</param>
    /// <param name="update">Whether update mode is on.</param>
    /// <param name="inCi">Whether this is a CI run; update mode is refused there.</param>
    /// <exception cref="GoldenMismatchException">The output differs, the file is missing, update mode just rewrote it, or update mode was asked for in CI.</exception>
    public static void Match(string actual, string sourceRoot, string relativePath, Normalization normalization, bool update, bool inCi)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(normalization);
        var path = Resolve(sourceRoot, relativePath);
        var normalized = normalization.Apply(actual);
        if (update)
        {
            if (inCi)
            {
                throw new GoldenMismatchException(
                    $"{UpdateVariable}=1 is refused in CI: golden files are updated locally and reviewed, never accepted by a CI run.");
            }

            // A file that already holds this output needs no rewrite and no review, so the run passes it: an update pass then
            // fails only on rows whose file it created or changed, and a failure of another kind stands out (#2236).
            if (File.Exists(path)
                && string.Equals(File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal), normalized, StringComparison.Ordinal))
            {
                return;
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!); // why: Resolve returns a full path under the root
            File.WriteAllText(path, normalized);
            throw new GoldenMismatchException(
                $"Golden file {path} updated from this output. Review the diff, then run again without {UpdateVariable}.");
        }

        if (!File.Exists(path))
        {
            throw new GoldenMismatchException(
                $"Golden file {path} is missing. Run with {UpdateVariable}=1 to create it from this output, then review the diff.");
        }

        var expected = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!string.Equals(expected, normalized, StringComparison.Ordinal))
        {
            throw new GoldenMismatchException(Describe(path, expected, normalized));
        }
    }

    internal static bool IsCi(Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return string.Equals(environment("CI"), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environment("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string SourceRootOf(Assembly testAssembly)
    {
        ArgumentNullException.ThrowIfNull(testAssembly);
        var root = testAssembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, SourceRootKey, StringComparison.Ordinal))?.Value;
        return string.IsNullOrEmpty(root)
            ? throw new InvalidOperationException(
                $"{testAssembly.GetName().Name} doesn't declare its source root. Add "
                + $"<AssemblyMetadata Include=\"{SourceRootKey}\" Value=\"$(MSBuildProjectDirectory)\" /> to its project file.")
            : root;
    }

    private static string Resolve(string sourceRoot, string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceRoot);
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException($"Golden path '{relativePath}' must be relative to the test project.", nameof(relativePath));
        }

        var root = Path.GetFullPath(sourceRoot);
        var full = Path.GetFullPath(Path.Combine(root, relativePath));
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.Ordinal)
            ? full
            : throw new ArgumentException($"Golden path '{relativePath}' leaves the test project ({root}).", nameof(relativePath));
    }

    private static string Describe(string goldenPath, string expected, string actual)
    {
        var want = expected.Split('\n');
        var got = actual.Split('\n');
        var line = 0;
        while (line < want.Length && line < got.Length && string.Equals(want[line], got[line], StringComparison.Ordinal))
        {
            line++;
        }

        var wantLine = line < want.Length ? want[line] : "<end of file>";
        var gotLine = line < got.Length ? got[line] : "<end of file>";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{goldenPath} differs at line {line + 1} (output normalized; the golden file is compared as written).\nexpected: {wantLine}\nactual:   {gotLine}\nRun with {UpdateVariable}=1 to accept the new output, then review the diff.");
    }
}
