using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// Turns a description or comment into lines of runs (Linear hub spec §3, "Issue sheet"). The text stays plain: only an http(s) address with a host and no
/// user name becomes a link (a bare address, or a named one with its host after the name), a picture becomes a bracketed placeholder and is never fetched, and
/// anything else, `sindri://` text included, stays as written.
/// </summary>
internal static partial class LinearText
{
    private const string TrailingMarks = ".,;:!?)]}'\"";

    [GeneratedRegex("""(?<md>(?<bang>!?)\[(?<label>[^\]\r\n]*)\]\((?<murl>[^)\s]*)\))|(?<bare>https?://[^\s<>"']+)""", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex Pattern { get; }

    /// <summary>Splits text into its lines, each as runs.</summary>
    /// <param name="text">The text.</param>
    /// <param name="open">What a link run calls to open its address.</param>
    /// <returns>One line per line of text, blank ones with no runs.</returns>
    internal static IReadOnlyList<LinearTextLine> Lines(string text, Func<Uri, CancellationToken, Task<bool>> open) =>
        [.. text.Split('\n').Select(line => new LinearTextLine(Runs(Clean(line.TrimEnd('\r')), open)))];

    // Control characters and the bidirectional overrides (which can reorder what a line says) are dropped.
    private static string Clean(string line) =>
        string.Concat(line.Where(c => !char.IsControl(c) && c is not ((>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069'))));

    private static List<LinearTextRun> Runs(string line, Func<Uri, CancellationToken, Task<bool>> open)
    {
        var runs = new List<LinearTextRun>();
        var at = 0;
        foreach (var match in Pattern.Matches(line).Cast<Match>())
        {
            Plain(runs, line[at..match.Index]);
            at = match.Index + match.Length;
            if (match.Groups["md"].Success)
            {
                Named(runs, match, open);
            }
            else
            {
                Bare(runs, match.Value, open);
            }
        }

        Plain(runs, line[at..]);
        return runs;
    }

    private static void Plain(List<LinearTextRun> runs, string text)
    {
        if (text.Length > 0)
        {
            runs.Add(new LinearTextRun(text));
        }
    }

    // [name](address) is a link showing its host; ![alt](address) is a picture: a placeholder, never fetched or linked. The host shows as IdnHost (punycode), so a lookalike such as Cyrillic "аpple.com" can't pass for the real site.
    private static void Named(List<LinearTextRun> runs, Match match, Func<Uri, CancellationToken, Task<bool>> open)
    {
        var label = match.Groups["label"].Value.Trim();
        var web = Web(match.Groups["murl"].Value);
        if (match.Groups["bang"].Length > 0)
        {
            Plain(runs, Labels.LinearImagePlaceholder(label, web?.IdnHost ?? string.Empty));
        }
        else if (web is null)
        {
            Plain(runs, match.Value);
        }
        else
        {
            runs.Add(new LinearTextRun(label.Length > 0 ? Labels.LinearNamedLink(label, web.IdnHost) : web.AbsoluteUri, web, open));
        }
    }

    // An address on its own: the sentence's punctuation after it is not part of it. A lookalike host shows as its punycode, as in Named.
    private static void Bare(List<LinearTextRun> runs, string text, Func<Uri, CancellationToken, Task<bool>> open)
    {
        var address = text.TrimEnd(TrailingMarks.ToCharArray());
        if (Web(address) is { } web)
        {
            runs.Add(new LinearTextRun(string.Equals(web.Host, web.IdnHost, StringComparison.Ordinal) ? address : new UriBuilder(web) { Host = web.IdnHost }.Uri.AbsoluteUri, web, open));
            Plain(runs, text[address.Length..]);
        }
        else
        {
            Plain(runs, text);
        }
    }

    // http or https, a host, and no user name (https://trusted.example@evil.example/ would read as the first host).
    private static Uri? Web(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))
        && uri.Host.Length > 0 && uri.UserInfo.Length == 0
            ? uri
            : null;
}
