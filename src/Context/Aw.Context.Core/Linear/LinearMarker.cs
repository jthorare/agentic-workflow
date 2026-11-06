using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>
/// The line Sindri puts at the end of its own Linear comment (spec 2026-11-04 §6.2): '&lt;!-- sindri:issue=ENG-12;gen=1 --&gt;'. A comment is Sindri's when its body carries this line
/// and its author is the write key's user (<see cref="IsSindri"/>), so a teammate who pastes the line isn't hidden.
/// </summary>
public static class LinearMarker
{
    /// <summary>What a marker line starts with.</summary>
    public const string Prefix = "<!-- sindri:issue=";

    /// <summary>What a marker line ends with.</summary>
    public const string Suffix = "-->";

    /// <summary>The marker line for an issue and the generation of its comment.</summary>
    /// <param name="identifier">The issue's identifier, 'ENG-12'.</param>
    /// <param name="generation">Which comment this is (a deleted comment is replaced by the next generation).</param>
    /// <returns>The line.</returns>
    public static string Line(string identifier, int generation) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Prefix}{identifier};gen={generation} {Suffix}");

    /// <summary>True when some line of <paramref name="body"/> is a marker line.</summary>
    /// <param name="body">A comment's body.</param>
    /// <returns>Whether it carries the marker.</returns>
    public static bool Carries(string? body) =>
        body is { Length: > 0 }
        && body.Split('\n').Any(static line => line.Trim() is var t && t.StartsWith(Prefix, StringComparison.Ordinal) && t.EndsWith(Suffix, StringComparison.Ordinal));

    /// <summary>True when the comment is Sindri's own: marker line and author both match.</summary>
    /// <param name="body">The comment's body.</param>
    /// <param name="authorId">The author's Linear user id, or null when Linear didn't say.</param>
    /// <param name="writeUserId">The write key's Linear user id; null when there is no write key (nothing is Sindri's then).</param>
    /// <returns>Whether the comment is Sindri's.</returns>
    public static bool IsSindri(string? body, ExternalId? authorId, ExternalId? writeUserId) =>
        writeUserId is { } write && authorId is { } author && author.Value == write.Value && Carries(body);
}
