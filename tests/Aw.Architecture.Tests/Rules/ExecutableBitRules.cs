using System.Text.RegularExpressions;

namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// A Unix mode is read in one place, Aw.Kernel.Data's FileModes (#2217, #2233): File.GetUnixFileMode follows a link, so on a dangling one it throws where the caller wants a yes, a no or "unknown".
/// Read from the source text: any line that calls GetUnixFileMode( outside FileModes.cs. File.SetUnixFileMode( alone is fine.
/// </summary>
public static partial class ExecutableBitRules
{
    /// <summary>A source file other than FileModes reads a mode with File.GetUnixFileMode.</summary>
    public const string Id = "ARCH-EXEC-001";

    [GeneratedRegex(@"\bGetUnixFileMode\(", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ModeRead { get; }

    /// <summary>Checks the source files.</summary>
    /// <param name="files">Each file's repo-relative path and its text.</param>
    /// <param name="allowed">The repo-relative path of the one file that may read a mode.</param>
    /// <returns>One violation per offending file.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<(string Path, string Text)> files, string allowed)
    {
        ArgumentNullException.ThrowIfNull(files);
        return files
            .Where(f => !string.Equals(f.Path, allowed, StringComparison.Ordinal) && ModeRead.IsMatch(f.Text))
            .Select(f => new Violation(Id, $"{f.Path} reads a mode with File.GetUnixFileMode",
                "call FileModes.TryRead (Aw.Kernel.Data): it answers null for a dangling link or an unreadable stat instead of throwing, and the caller turns null into its own row or refusal"));
    }
}
