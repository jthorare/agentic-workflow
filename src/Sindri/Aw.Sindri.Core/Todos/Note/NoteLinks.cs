using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Todos.Note;

/// <summary>The 'sindri://' links a todo line may hold (todo spec D10, §4.5): items, PRs and Linear issues. Any other kind is a pasted link as far as the note goes.</summary>
internal static partial class NoteLinks
{
    /// <summary>True for a deep link of a kind the note writes.</summary>
    /// <param name="url">The link.</param>
    /// <returns>True when it is an item, PR or Linear link.</returns>
    public static bool IsTodoLink(string url) => Kind.IsMatch(url);

    /// <summary>The label the note shows for a link: 'PR #12', 'ENG-12' or 'ITM-1'.</summary>
    /// <param name="url">A link <see cref="IsTodoLink"/> accepts.</param>
    /// <returns>The label.</returns>
    public static string Label(string url)
    {
        var match = Kind.Match(url);
        return match.Groups["pr"].Success ? "PR #" + match.Groups["pr"].Value
            : match.Groups["linear"].Success ? match.Groups["linear"].Value
            : match.Groups["item"].Value;
    }

    /// <summary>The links of a todo that the note can show, in order.</summary>
    /// <param name="links">All the todo's links.</param>
    /// <returns>The ones <see cref="IsTodoLink"/> accepts.</returns>
    public static IReadOnlyList<string> Renderable(IEnumerable<string> links) => [.. links.Where(IsTodoLink)];

    [GeneratedRegex(
        "^sindri://(?:reviews/[^/\\s()\\[\\]]+/[^/\\s()\\[\\]]+/(?<pr>[0-9]+)|linear/(?<linear>[A-Za-z][A-Za-z0-9]*-[0-9]+)|item/(?<item>[A-Za-z0-9][A-Za-z0-9-]*))\\z",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex Kind { get; }
}
