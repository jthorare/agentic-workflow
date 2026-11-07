using System.Text.RegularExpressions;

namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// Whether a path is an executable file is asked in one place, Aw.Kernel.Data's ExecutableFiles (#2217): File.GetUnixFileMode follows a link, so on a dangling one it throws where the caller wants a yes or a no.
/// Read from the source text: a line that masks the mode File.GetUnixFileMode returns (with &amp; or HasFlag) against a name containing "Execute". A mode compared whole (FleetStateFiles) or masked against group and other
/// read and write bits (the token and launcher file checks) is not an execute test.
/// </summary>
public static partial class ExecutableBitRules
{
    /// <summary>A source file other than ExecutableFiles tests an execute bit of File.GetUnixFileMode.</summary>
    public const string Id = "ARCH-EXEC-001";

    [GeneratedRegex(@"GetUnixFileMode\([^;\r\n]*?\)\s*(?:&|\.HasFlag\()[^;\r\n]*Execute", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ExecuteTest { get; }

    /// <summary>Checks the source files.</summary>
    /// <param name="files">Each file's repo-relative path and its text.</param>
    /// <param name="allowed">The repo-relative path of the one file that may test the bits.</param>
    /// <returns>One violation per offending file.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<(string Path, string Text)> files, string allowed)
    {
        ArgumentNullException.ThrowIfNull(files);
        return files
            .Where(f => !string.Equals(f.Path, allowed, StringComparison.Ordinal) && ExecuteTest.IsMatch(f.Text))
            .Select(f => new Violation(Id, $"{f.Path} tests an execute bit of File.GetUnixFileMode",
                "call ExecutableFiles.IsExecutable (Aw.Kernel.Data): it answers false for a dangling link or an unreadable stat instead of throwing"));
    }
}
