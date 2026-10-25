using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Ids;

namespace Aw.Sindri.Core.Commands;

/// <summary>
/// A catalog row's CLI form (UI spec E-16): 'sindri' and checked parts only, so a copied command can't carry a newline,
/// a bidi override or a second command. Parts are lower-case words, a fixed '--flag', a '&lt;placeholder&gt;' (or '&lt;id&gt;@&lt;hash&gt;', two of them joined by an '@') or an
/// '[optional part]' made of those; no control, bidi or invisible character anywhere. <see cref="Fill"/> puts typed
/// ids in place of placeholders, and only ids whose text is safe on a command line.
/// </summary>
public sealed partial record CliForm
{
    private CliForm(string text)
    {
        Text = text;
    }

    /// <summary>The form, for example 'sindri profile approve &lt;hash&gt;'.</summary>
    public string Text { get; }

    /// <summary>Checks <paramref name="text"/> against the CLI form's shape.</summary>
    /// <param name="text">The form.</param>
    /// <returns>The form, or SND-CLI-003 when it has a hidden character or a part outside the shape.</returns>
    public static Result<CliForm> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Hidden(text) is { } hidden)
        {
            return hidden;
        }

        return Shape.IsMatch(text)
            ? new CliForm(text)
            : SindriErrors.InvalidInput.With($"'{text}' is not a CLI form: 'sindri' then words, --flags, <placeholders> or [optional parts] of those");
    }

    /// <summary>The form with the typed id <paramref name="id"/> in place of '&lt;<paramref name="placeholder"/>&gt;' (P4's NeedsYouRow.cli).</summary>
    /// <param name="placeholder">The placeholder's name, for example 'itemId'.</param>
    /// <param name="id">The id to substitute.</param>
    /// <typeparam name="TId">A typed id over text.</typeparam>
    /// <returns>The filled form, or SND-CLI-003 when the form has no such placeholder or the id's text isn't safe on a command line.</returns>
    public Result<CliForm> Fill<TId>(string placeholder, TId id)
        where TId : struct, ITypedId<TId, string>
    {
        ArgumentNullException.ThrowIfNull(placeholder);
        var token = "<" + placeholder + ">";
        var parts = Text.Split(' ');
        if (!parts.Any(part => part.Contains(token, StringComparison.Ordinal)))
        {
            return SindriErrors.InvalidInput.With($"'{Text}' has no <{Printable(placeholder)}> to fill");
        }

        if (id.Value is not { } value || !SafeValue.IsMatch(value))
        {
            return SindriErrors.InvalidInput.With(
                $"The value for <{Printable(placeholder)}> isn't safe on a command line: letters, digits, '.', '_', ':' and '-' only, starting with a letter or digit");
        }

        var filled = parts.Select(part => part.Replace(token, value, StringComparison.Ordinal));
        return new CliForm(string.Join(' ', filled));
    }

    /// <inheritdoc />
    public override string ToString() => Text;

    /// <summary>SND-CLI-003 naming the first control, bidi or invisible character (by code point, never the raw text), or null.</summary>
    private static Error? Hidden(string text)
    {
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsHidden(rune))
            {
                return SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture,
                    $"A CLI form can't hold control, bidi or invisible characters: U+{rune.Value:X4} at index {index}"));
            }

            index += rune.Utf16SequenceLength;
        }

        return null;
    }

    private static bool IsHidden(Rune rune) => rune.Value != ' ' && Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control
        or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.SpaceSeparator
        or UnicodeCategory.PrivateUse or UnicodeCategory.Surrogate or UnicodeCategory.OtherNotAssigned;

    /// <summary>A placeholder name for an error message: itself when it is a plain name, else a fixed word.</summary>
    private static string Printable(string placeholder) => Placeholder.IsMatch(placeholder) ? placeholder : "that placeholder";

    private const string Part = "(?:[a-z][a-z-]*|--[a-z][a-z-]*|<[a-zA-Z]+>(?:@<[a-zA-Z]+>)?)";

    [GeneratedRegex("^sindri(?: " + Part + "| \\[" + Part + "(?: " + Part + ")*\\])*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,199}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SafeValue { get; }

    [GeneratedRegex("^[a-zA-Z]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder { get; }
}
