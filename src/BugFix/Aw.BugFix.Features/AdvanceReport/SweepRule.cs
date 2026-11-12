using System.Text.RegularExpressions;
using Aw.BugFix.Core.Errors;
using Aw.Kernel;

namespace Aw.BugFix.Features.AdvanceReport;

/// <summary>
/// The sweep gate of a resolved report: the "Where else we checked" table must have a valid status on every row, and a
/// non-empty body unless the Prevention decision is an existing guard or deferred.
/// </summary>
internal static partial class SweepRule
{
    private const string TableHeading = "## Where else we checked";
    private const string DecisionHeading = "## Prevention decision";

    /// <summary>Checks a sweep file's text.</summary>
    /// <param name="markdown">The sweep file.</param>
    /// <returns>Success, or AW-BUGFIX-024 naming the problem.</returns>
    internal static Result<Unit> Check(string markdown)
    {
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        var rows = BodyRows(Section(lines, TableHeading));
        foreach (var row in rows)
        {
            var cells = row.Trim().Trim('|').Split('|').Select(static c => c.Trim()).ToArray();
            if (cells.Length < 3 || !ValidStatus(cells[2]))
            {
                return BugFixErrors.SweepRefused.With(
                    $"sweep row \"{row.Trim()}\" needs a status of fixed, not affected (<why>) or follow-up <link> (columns: Surface | Variant | Status)");
            }
        }

        if (rows.Count > 0 || Exempt(Section(lines, DecisionHeading)))
        {
            return Results.Ok();
        }

        return BugFixErrors.SweepRefused.With(
            "the \"Where else we checked\" table has no rows and the Prevention decision is not an existing guard or deferred; list every surface and variant the sweep covered (see skills/_shared/prevent-the-class.md)");
    }

    private static List<string> Section(string[] lines, string heading)
    {
        var start = Array.FindIndex(lines, l => string.Equals(l.Trim(), heading, StringComparison.OrdinalIgnoreCase));
        return start < 0
            ? []
            : [.. lines.Skip(start + 1).TakeWhile(static l => !l.StartsWith("## ", StringComparison.Ordinal))];
    }

    // The first row is the header, the second the separator; both are skipped. A table without a separator row has no body.
    private static List<string> BodyRows(List<string> section) =>
        [.. section.Where(static l => l.Trim().StartsWith('|')).Skip(2)];

    private static bool Exempt(List<string> decision)
    {
        var first = decision.Find(static l => l.Trim().Length > 0)?.Trim().TrimStart('-', '*', ' ') ?? string.Empty;
        return first.StartsWith("existing guard", StringComparison.OrdinalIgnoreCase)
            || first.StartsWith("deferred", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValidStatus(string status)
    {
        try
        {
            return Status.IsMatch(status);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;   // a status that can't be read in time is not accepted
        }
    }

    [GeneratedRegex(@"^(fixed|not affected \(\s*\S[^)]*\)|follow-up\s+\S.*)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Status { get; }
}
