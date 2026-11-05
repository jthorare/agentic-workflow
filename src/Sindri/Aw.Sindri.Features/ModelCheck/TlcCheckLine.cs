using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Features.ModelCheck;

/// <summary>
/// One 'tlc-check: &lt;Name&gt;/&lt;cfg&gt;: &lt;RESULT&gt; ...' line of the runner (scripts/tlc-check.sh) with the '  | ' lines under it. The result word decides; a word the step doesn't know is <see cref="TlcResult.Error"/>.
/// </summary>
/// <param name="Name">The spec folder's name.</param>
/// <param name="Cfg">The cfg, 'Takeover' or 'gaps/FullTimeout' (without '.cfg').</param>
/// <param name="Result">The result.</param>
/// <param name="Rest">What follows the result word.</param>
/// <param name="Detail">The indented lines under it (TLC's output), without the '  | ' margin.</param>
internal sealed record TlcCheckLine(string Name, string Cfg, TlcResult Result, string Rest, IReadOnlyList<string> Detail)
{
    private const string Prefix = "tlc-check: ";

    private static readonly HashSet<string> _known = new(["PASS", "VIOLATION", "TIMEOUT", "WITNESS-CAUGHT", "WITNESS-MISSED", "WITNESS-WRONG", "ERROR"], StringComparer.Ordinal);

    private static readonly Regex _counts = new(@"distinct=(?<distinct>\d+)\s+depth=(?<depth>\d+)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    /// <summary>The label the runner prints: 'Name/cfg'.</summary>
    internal string Label => $"{Name}/{Cfg}";

    /// <summary>The distinct states of a PASS, or 0.</summary>
    internal int Distinct => Count("distinct");

    /// <summary>The depth of a PASS, or 0.</summary>
    internal int Depth => Count("depth");

    /// <summary>Whether the check is a gaps/ witness.</summary>
    internal bool IsWitness => Cfg.StartsWith("gaps/", StringComparison.Ordinal);

    /// <summary>Reads the runner's output.</summary>
    /// <param name="output">All the runner printed.</param>
    /// <returns>One entry per 'tlc-check:' line, in order; other lines are not part of it.</returns>
    internal static List<TlcCheckLine> ParseAll(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var found = new List<TlcCheckLine>();
        List<string>? detail = null;
        foreach (var raw in output.Split('\n').Select(static l => l.TrimEnd('\r')))
        {
            if (raw.StartsWith(Prefix, StringComparison.Ordinal) && One(raw[Prefix.Length..]) is { } line)
            {
                detail = [];
                found.Add(line with { Detail = detail });
            }
            else if (detail is not null && raw.StartsWith("  | ", StringComparison.Ordinal))
            {
                detail.Add(raw[4..]);
            }
        }

        return found;
    }

    private static TlcCheckLine? One(string text)
    {
        var colon = text.IndexOf(": ", StringComparison.Ordinal);
        var slash = text.IndexOf('/', StringComparison.Ordinal);
        if (colon < 0 || slash < 1 || slash > colon)
        {
            return null;   // a runner warning ('tlc-check: Name: warning: ...') or text that is not a check
        }

        var after = text[(colon + 2)..].TrimStart();
        var space = after.IndexOf(' ', StringComparison.Ordinal);
        var word = space < 0 ? after : after[..space];
        var rest = space < 0 ? string.Empty : after[(space + 1)..].Trim();
        return new TlcCheckLine(text[..slash], text[(slash + 1)..colon], Classify(word), _known.Contains(word) ? rest : $"{word} {rest}".Trim(), []);
    }

    private static TlcResult Classify(string word) => word switch
    {
        "PASS" => TlcResult.Pass,
        "VIOLATION" => TlcResult.Violation,
        "TIMEOUT" => TlcResult.Timeout,
        "WITNESS-CAUGHT" => TlcResult.WitnessCaught,
        "WITNESS-MISSED" => TlcResult.WitnessMissed,
        "WITNESS-WRONG" => TlcResult.WitnessWrong,
        _ => TlcResult.Error,
    };

    private int Count(string group) => _counts.Match(Rest) is { Success: true } match ? int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture) : 0;
}
