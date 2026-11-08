using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Todos;

/// <summary>A todo's text rules (todo spec D3): one line of 1 to 280 characters, no Control or Format character, no line or paragraph separator, none of the note's markers and no markdown link.</summary>
public static partial class TodoText
{
    /// <summary>The longest text, in characters (Unicode scalar values, so an emoji counts once).</summary>
    public const int MaxLength = 280;

    /// <summary>Checks <paramref name="text"/>.</summary>
    /// <param name="text">The typed text.</param>
    /// <returns>The trimmed text, SND-TODO-001 for an empty or over-long one, or SND-TODO-007 for a forbidden character, marker or link.</returns>
    public static Result<string> Check(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return SindriErrors.TodoText.With("Add the todo's text.");
        }

        var runes = 0;
        var hidden = false;
        foreach (var rune in trimmed.EnumerateRunes())
        {
            runes++;
            hidden |= IsForbidden(rune);
        }

        if (runes > MaxLength)
        {
            return SindriErrors.TodoText.With(string.Create(CultureInfo.InvariantCulture, $"A todo is one line of 1 to {MaxLength} characters; this one has {runes}."));
        }

        if (hidden)
        {
            return SindriErrors.TodoForbidden.With("A todo can't contain hidden or control characters.");
        }

        return HasMarker(trimmed)
            ? SindriErrors.TodoForbidden.With("A todo can't contain 📅, ✅, ^td- or a markdown link: those belong to the note.")
            : trimmed;
    }

    private static bool IsForbidden(Rune rune) => Rune.GetUnicodeCategory(rune) is
        UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;

    private static bool HasMarker(string text) =>
        text.Contains("📅", StringComparison.Ordinal)
        || text.Contains('✅')
        || text.Contains("^td-", StringComparison.Ordinal)
        || MarkdownLink.IsMatch(text);

    [GeneratedRegex("\\[[^\\]]*\\]\\([^)]*\\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MarkdownLink { get; }
}
