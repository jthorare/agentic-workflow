using System.Globalization;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Checks;

/// <summary>The staged secret scan (SND-SCRUB-002): a line the scrubber would change holds a secret. A hit names the file and line, never the match.</summary>
public static class SecretScan
{
    /// <summary>The findings for the lines that hold a secret.</summary>
    /// <param name="rule">The rule id to report (SND-SCRUB-002).</param>
    /// <param name="lines">The lines the branch added.</param>
    /// <param name="finder">Recognises secrets (Aw.Redaction's patterns) and names the rule and value of each hit.</param>
    /// <returns>One finding per line with a secret.</returns>
    public static IReadOnlyList<RuleFinding> Findings(string rule, IReadOnlyList<AddedLine> lines, ISecretFinder finder)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(finder);
        return [.. lines
            .Where(l => HoldsSecret(finder, l.Text))
            .Select(l => new RuleFinding(rule, string.Create(CultureInfo.InvariantCulture, $"a secret on an added line of {l.Path}:{l.Line} (remove it and rotate it; nothing is pushed)"), l.Path, l.Line))];
    }

    // A line is clean unless some hit could be a credential: only a secret-assignment to a *SECRET key whose value is shaped like a resource name is a pointer (#2644). A blocked scan fails closed.
    private static bool HoldsSecret(ISecretFinder finder, string text)
    {
        var hits = finder.Find(text, out var complete);
        return !complete || hits.Any(static h => !(string.Equals(h.Rule, "secret-assignment", StringComparison.Ordinal) && ResourceName.KeyNamesASecret(h.Key) && ResourceName.Looks(h.Value)));
    }
}
