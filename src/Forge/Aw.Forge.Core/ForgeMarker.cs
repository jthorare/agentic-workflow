using Aw.Kernel;

namespace Aw.Forge.Core;

/// <summary>
/// The idempotency marker a reply, reaction or PR body carries. It travels as a hidden HTML comment appended to the text,
/// so an adapter finds out whether it already delivered by reading the object back (no local state needed).
/// </summary>
public static class ForgeMarker
{
    private const string Prefix = "<!-- aw-marker:";
    private const string Suffix = " -->";

    /// <summary>Checks a marker: 1 to 128 characters of letters, digits, '.', '_', ':' and '-'.</summary>
    /// <param name="marker">The marker.</param>
    /// <returns>Success, or the failure naming the rule.</returns>
    public static Result<Unit> Validate(string? marker)
    {
        if (string.IsNullOrEmpty(marker) || marker.Length > ForgeLimits.MarkerCharacters)
        {
            return ForgeErrors.Invalid.With($"marker: 1 to {ForgeLimits.MarkerCharacters} characters");
        }

        foreach (var c in marker)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or ':' or '-'))
            {
                return ForgeErrors.Invalid.With("marker: letters, digits, '.', '_', ':' and '-' only");
            }
        }

        return Results.Ok();
    }

    /// <summary>The hidden comment for a marker.</summary>
    /// <param name="marker">A valid marker.</param>
    /// <returns>The comment text.</returns>
    public static string Tag(string marker) => Prefix + marker + Suffix;

    /// <summary>Appends the marker's hidden comment to a text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="marker">A valid marker.</param>
    /// <returns>The text with the marker.</returns>
    public static string Append(string text, string marker) => text.TrimEnd() + "\n\n" + Tag(marker);

    /// <summary>True when a text carries the marker.</summary>
    /// <param name="text">The text.</param>
    /// <param name="marker">A valid marker.</param>
    /// <returns>Whether the marker's comment is in the text.</returns>
    public static bool IsIn(string? text, string marker) => text is not null && text.Contains(Tag(marker), StringComparison.Ordinal);

    /// <summary>Every marker in a text, in order.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The markers.</returns>
    public static IReadOnlyList<string> Of(string? text)
    {
        var found = new List<string>();
        var from = 0;
        while (text is not null && text.IndexOf(Prefix, from, StringComparison.Ordinal) is var at and >= 0)
        {
            var start = at + Prefix.Length;
            var end = text.IndexOf(Suffix, start, StringComparison.Ordinal);
            if (end < 0)
            {
                break;
            }

            found.Add(text[start..end]);
            from = end + Suffix.Length;
        }

        return found;
    }
}
