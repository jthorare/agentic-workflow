using System.Globalization;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Checks;

/// <summary>BLD-SECRET-001 and SHIP-SECRET-001 (line spec §7.2, §7.5): a line the scrubber would change holds a secret. A hit names the file and line, never the match.</summary>
public static class SecretScan
{
    /// <summary>The findings for the lines that hold a secret.</summary>
    /// <param name="rule">The rule id to report ('BLD-SECRET-001' or 'SHIP-SECRET-001').</param>
    /// <param name="lines">The lines the branch added.</param>
    /// <param name="scrubber">Recognises secrets (Aw.Redaction's patterns).</param>
    /// <returns>One finding per line with a secret.</returns>
    public static IReadOnlyList<RuleFinding> Findings(string rule, IReadOnlyList<AddedLine> lines, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(scrubber);
        return [.. lines
            .Where(l => !string.Equals(scrubber.Scrub(l.Text), l.Text, StringComparison.Ordinal))
            .Select(l => new RuleFinding(rule, string.Create(CultureInfo.InvariantCulture, $"a secret on an added line of {l.Path}:{l.Line} (remove it and rotate it; nothing is pushed)"), l.Path, l.Line))];
    }
}
