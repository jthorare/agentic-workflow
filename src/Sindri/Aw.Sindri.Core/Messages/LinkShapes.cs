namespace Aw.Sindri.Core.Messages;

/// <summary>
/// The url shapes a link in message text resolves from (messages spec §9.3, #3021), each to the row key Home uses (Home spec §3.1,
/// '&lt;source&gt;:&lt;kind&gt;:&lt;source id&gt;'): a GitHub pull request to 'gh:pr:&lt;owner&gt;/&lt;repo&gt;#&lt;n&gt;', a GitHub issue to
/// 'gh:issue:&lt;owner&gt;/&lt;repo&gt;#&lt;n&gt;' and a Linear issue to 'linear:issue:&lt;KEY-n&gt;'. Any other url has no key. Pure: no call leaves.
/// </summary>
public static class LinkShapes
{
    /// <summary>The most urls one resolve asks about (one window's links).</summary>
    public const int MaxUrls = 20;

    private const int MaxNumberDigits = 9;

    /// <summary>The row key <paramref name="url"/> stands for, or null when its shape is none of the known ones.</summary>
    /// <param name="url">The url as the message holds it.</param>
    /// <returns>The row key, or null.</returns>
    public static string? RowKey(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri || url.Scheme is not ("https" or "http"))
        {
            return null;
        }

        var parts = url.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return url.Host switch
        {
            "github.com" or "www.github.com" => GitHub(parts),
            "linear.app" => Linear(parts),
            _ => null,
        };
    }

    // <owner>/<repo>/pull/<n> or <owner>/<repo>/issues/<n>, with anything after (a PR's /files tab)
    private static string? GitHub(string[] parts)
    {
        if (parts.Length < 4 || !IsName(parts[0]) || !IsName(parts[1]) || !IsNumber(parts[3]))
        {
            return null;
        }

        return parts[2] switch
        {
            "pull" => $"gh:pr:{parts[0]}/{parts[1]}#{parts[3]}",
            "issues" => $"gh:issue:{parts[0]}/{parts[1]}#{parts[3]}",
            _ => null,
        };
    }

    // <workspace>/issue/<KEY-n>[/<slug>]
    private static string? Linear(string[] parts) =>
        parts.Length >= 3 && string.Equals(parts[1], "issue", StringComparison.Ordinal) && IsIssueKey(parts[2])
            ? "linear:issue:" + parts[2].ToUpperInvariant()
            : null;

    // a GitHub owner or repo name: letters, digits, '-', '_' and '.', not starting with '.'
    private static bool IsName(string part) =>
        part.Length is > 0 and <= 100 && part[0] != '.' && part.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    private static bool IsNumber(string part) =>
        part.Length is > 0 and <= MaxNumberDigits && part[0] != '0' && part.All(char.IsAsciiDigit);

    // 'ENG-412': a team key of letters and digits starting with a letter, a '-', then the number
    private static bool IsIssueKey(string part)
    {
        var dash = part.IndexOf('-', StringComparison.Ordinal);
        return dash is > 0 and <= 10
            && char.IsAsciiLetter(part[0])
            && part[..dash].All(char.IsAsciiLetterOrDigit)
            && IsNumber(part[(dash + 1)..]);
    }
}
