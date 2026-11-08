using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Todos;

/// <summary>A todo's links (todo spec §4.3): items, PRs and Linear issues, each stored as the deep link the app opens. One parser per kind, one list of kinds, so a new kind is a row here.</summary>
public static partial class TodoLinks
{
    /// <summary>The most links a todo takes.</summary>
    public const int Max = 10;

    private delegate string? KindParser(string typed, IReadOnlyCollection<string> forgeHosts);

    // One row per link kind: a new kind is a parser added here.
    private static readonly KindParser[] _kinds = [ItemLink, ShortPrLink, PrUrlLink, LinearLink];

    /// <summary>Reads one typed link: 'ITM-…' (to 'sindri://item/…'), '&lt;owner&gt;/&lt;repo&gt;#&lt;n&gt;' or an https PR url on a configured forge host (to 'sindri://reviews/&lt;owner&gt;/&lt;repo&gt;/&lt;n&gt;'), or a Linear key 'ENG-12' (to 'sindri://linear/ENG-12'). Checks the shape only.</summary>
    /// <param name="typed">The typed link.</param>
    /// <param name="forgeHosts">The forge hosts a PR url may name.</param>
    /// <returns>The deep link, or SND-TODO-004.</returns>
    public static Result<string> Parse(string typed, IReadOnlyCollection<string> forgeHosts)
    {
        ArgumentNullException.ThrowIfNull(typed);
        ArgumentNullException.ThrowIfNull(forgeHosts);
        var word = typed.Trim();
        foreach (var kind in _kinds)
        {
            if (kind(word, forgeHosts) is { } link)
            {
                return link;
            }
        }

        return SindriErrors.TodoLink.With($"'{typed}' isn't an item, PR, Linear key or workflow.");
    }

    /// <summary>Reads a whole list: each link parsed, duplicates dropped, at most <see cref="Max"/>.</summary>
    /// <param name="typed">The typed links.</param>
    /// <param name="forgeHosts">The forge hosts a PR url may name.</param>
    /// <returns>The deep links in order, or SND-TODO-004.</returns>
    public static Result<IReadOnlyList<string>> ParseAll(IReadOnlyList<string> typed, IReadOnlyCollection<string> forgeHosts)
    {
        ArgumentNullException.ThrowIfNull(typed);
        ArgumentNullException.ThrowIfNull(forgeHosts);
        var links = new List<string>();
        foreach (var one in typed)
        {
            var parsed = Parse(one, forgeHosts);
            if (parsed.IsError)
            {
                return parsed.Error;
            }

            if (!links.Contains(parsed.Value, StringComparer.Ordinal))
            {
                links.Add(parsed.Value);
            }
        }

        return links.Count > Max
            ? SindriErrors.TodoLink.With($"A todo takes at most {Max} links.")
            : links;
    }

    /// <summary>Describes one stored deep link for the wire: its kind, the short reference a chip shows and the deep link itself.</summary>
    /// <param name="deepLink">A stored deep link: 'sindri://item/…', 'sindri://reviews/&lt;owner&gt;/&lt;repo&gt;/&lt;n&gt;', 'sindri://linear/…' or 'sindri://workflow/…'.</param>
    /// <returns>Kind 'item', 'pr', 'linear' or 'workflow'; any other shape is kind 'link' with the deep link as its reference.</returns>
    public static (string Kind, string Ref, string Href) Describe(string deepLink)
    {
        ArgumentNullException.ThrowIfNull(deepLink);
        if (deepLink.StartsWith("sindri://item/", StringComparison.Ordinal) && deepLink.Length > "sindri://item/".Length)
        {
            return ("item", deepLink["sindri://item/".Length..], deepLink);
        }

        if (deepLink.StartsWith("sindri://linear/", StringComparison.Ordinal) && deepLink.Length > "sindri://linear/".Length)
        {
            return ("linear", deepLink["sindri://linear/".Length..], deepLink);
        }

        if (deepLink.StartsWith("sindri://workflow/", StringComparison.Ordinal) && deepLink.Length > "sindri://workflow/".Length)
        {
            return ("workflow", deepLink["sindri://workflow/".Length..], deepLink);
        }

        var review = ReviewLink.Match(deepLink);
        return review.Success
            ? ("pr", $"{review.Groups["owner"].Value}/{review.Groups["repo"].Value}#{review.Groups["n"].Value}", deepLink)
            : ("link", deepLink, deepLink);
    }

    private static string? ItemLink(string typed, IReadOnlyCollection<string> forgeHosts) =>
        ItemShape.IsMatch(typed) ? "sindri://item/" + typed : null;

    private static string? ShortPrLink(string typed, IReadOnlyCollection<string> forgeHosts)
    {
        var match = ShortPr.Match(typed);
        return match.Success && IsName(match.Groups["owner"].Value) && IsName(match.Groups["repo"].Value)
            ? Review(match.Groups["owner"].Value, match.Groups["repo"].Value, match.Groups["n"].Value)
            : null;
    }

    private static string? PrUrlLink(string typed, IReadOnlyCollection<string> forgeHosts)
    {
        if (!Uri.TryCreate(typed, UriKind.Absolute, out var url)
            || !string.Equals(url.Scheme, "https", StringComparison.Ordinal)
            || url.UserInfo.Length > 0
            || !url.IsDefaultPort
            || !forgeHosts.Contains(url.Host, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        var match = PrPath.Match(url.AbsolutePath);
        return match.Success && IsName(match.Groups["owner"].Value) && IsName(match.Groups["repo"].Value)
            ? Review(match.Groups["owner"].Value, match.Groups["repo"].Value, match.Groups["n"].Value)
            : null;
    }

    private static string? LinearLink(string typed, IReadOnlyCollection<string> forgeHosts) =>
        LinearKey.IsMatch(typed) ? "sindri://linear/" + typed : null;

    private static string Review(string owner, string repo, string number) =>
        string.Create(CultureInfo.InvariantCulture, $"sindri://reviews/{owner}/{repo}/{long.Parse(number, CultureInfo.InvariantCulture)}");

    private static bool IsName(string part) => !string.Equals(part, ".", StringComparison.Ordinal) && !string.Equals(part, "..", StringComparison.Ordinal);

    [GeneratedRegex("^sindri://reviews/(?<owner>[^/]+)/(?<repo>[^/]+)/(?<n>[0-9]+)\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReviewLink { get; }

    [GeneratedRegex("^ITM-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ItemShape { get; }

    [GeneratedRegex("^(?<owner>[A-Za-z0-9._-]+)/(?<repo>[A-Za-z0-9._-]+)#(?<n>[0-9]{1,9})\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ShortPr { get; }

    [GeneratedRegex("^/(?<owner>[A-Za-z0-9._-]+)/(?<repo>[A-Za-z0-9._-]+)/pull/(?<n>[0-9]{1,9})/?\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PrPath { get; }

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,9}-[0-9]{1,9}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LinearKey { get; }
}
