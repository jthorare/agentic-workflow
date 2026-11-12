using System.Text.RegularExpressions;
using Aw.BugFix.Core.Errors;
using Aw.Kernel;

namespace Aw.BugFix.Features.AdvanceReport;

/// <summary>
/// The sweep gate of a resolved report: the "Where else we checked" table must have a separator row and a valid status on
/// every row, the Prevention decision must start with "existing guard", "new mechanism" or "deferred", and the
/// table needs a body unless the decision is an existing guard or deferred.
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
        var table = Section(lines, TableHeading).Where(static l => l.Trim().StartsWith('|')).ToList();
        if (table.Count > 0 && (table.Count < 2 || !Separator.IsMatch(table[1].Trim())))
        {
            return BugFixErrors.SweepRefused.With(
                "the \"Where else we checked\" table has no separator row (| --- | --- | --- |) under its header row");
        }

        var rows = table.Skip(2).ToList();
        foreach (var row in rows)
        {
            var cells = row.Trim().Trim('|').Split('|').Select(static c => c.Trim()).ToArray();
            if (cells.Length < 3 || !ValidStatus(cells[2]))
            {
                return BugFixErrors.SweepRefused.With(
                    $"sweep row \"{row.Trim()}\" needs a status of fixed, not affected (<why>) or follow-up <link> (columns: Surface | Variant | Status)");
            }
        }

        var decision = Decision(Section(lines, DecisionHeading));
        if (decision is null)
        {
            return BugFixErrors.SweepRefused.With(
                "the Prevention decision is missing or does not start with existing guard, new mechanism or deferred (see skills/_shared/prevent-the-class.md)");
        }

        if (rows.Count > 0 || decision != DecisionKind.Mechanism)
        {
            return Results.Ok();
        }

        return BugFixErrors.SweepRefused.With(
            "the \"Where else we checked\" table has no rows and the Prevention decision is not an existing guard or deferred; list every surface and variant the sweep covered (see skills/_shared/prevent-the-class.md)");
    }

    private enum DecisionKind
    {
        Mechanism,
        Exempt,
    }

    private static List<string> Section(string[] lines, string heading)
    {
        var start = Array.FindIndex(lines, l => string.Equals(l.Trim(), heading, StringComparison.OrdinalIgnoreCase));
        return start < 0
            ? []
            : [.. lines.Skip(start + 1).TakeWhile(static l => !l.StartsWith("## ", StringComparison.Ordinal))];
    }

    // The first words of the Prevention decision: Exempt for an existing guard or deferred, Mechanism for a new mechanism, null for anything else.
    private static DecisionKind? Decision(List<string> decision)
    {
        var first = decision.Find(static l => l.Trim().Length > 0)?.Trim().TrimStart('-', '*', ' ') ?? string.Empty;
        if (first.StartsWith("existing guard", StringComparison.OrdinalIgnoreCase)
            || first.StartsWith("deferred", StringComparison.OrdinalIgnoreCase))
        {
            return DecisionKind.Exempt;
        }

        return first.StartsWith("new mechanism", StringComparison.OrdinalIgnoreCase) ? DecisionKind.Mechanism : null;
    }

    private static bool ValidStatus(string status)
    {
        try
        {
            return Status.IsMatch(status) && (!status.StartsWith("not affected", StringComparison.OrdinalIgnoreCase) || BalancedReason(status));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;   // a status that can't be read in time is not accepted
        }
    }

    // "not affected (reason)": the reason may hold balanced parentheses, and the first "(" closes only at the very end.
    private static bool BalancedReason(string status)
    {
        var depth = 0;
        for (var i = status.IndexOf('(', StringComparison.Ordinal); i < status.Length; i++)
        {
            depth += status[i] == '(' ? 1 : status[i] == ')' ? -1 : 0;
            if (depth < 0 || (depth == 0 && i < status.Length - 1))
            {
                return false;
            }
        }

        return depth == 0;
    }

    [GeneratedRegex(@"^(fixed|not affected \(\s*\S.*\)|follow-up\s+\S.*)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Status { get; }

    [GeneratedRegex(@"^\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Separator { get; }
}
