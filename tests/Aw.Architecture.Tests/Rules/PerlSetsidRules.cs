using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Architecture.Tests.Rules;

/// <summary>
/// Perl's POSIX::setsid() returns -1 on failure, and -1 is true in perl, so "or die" never fires and the child runs undetached without a sign (#2694).
/// Read from the source text: every POSIX::setsid() in a line that is not a comment must be followed by == -1.
/// </summary>
public static partial class PerlSetsidRules
{
    /// <summary>A source file calls POSIX::setsid() without checking for -1.</summary>
    public const string Id = "ARCH-SETSID-001";

    [GeneratedRegex(@"POSIX::setsid\(\)(?!\s*==\s*-1)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UncheckedCall { get; }

    /// <summary>Checks the source files.</summary>
    /// <param name="files">Each file's repo-relative path and its text.</param>
    /// <returns>One violation per unchecked call.</returns>
    public static IEnumerable<Violation> Check(IEnumerable<(string Path, string Text)> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var found = new List<Violation>();
        foreach (var (path, text) in files)
        {
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].AsSpan().TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var _ in UncheckedCall.Matches(lines[i]))
                {
                    found.Add(new Violation(Id, string.Create(CultureInfo.InvariantCulture, $"{path}:{i + 1} calls POSIX::setsid() without checking for -1"),
                        "write \"POSIX::setsid() == -1 and die \\\"setsid: $!\\\\n\\\"\" : setsid returns -1 (true in perl) on failure, so \"or die\" never fires"));
                }
            }
        }

        return found;
    }
}
