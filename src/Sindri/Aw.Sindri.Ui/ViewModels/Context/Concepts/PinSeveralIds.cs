using System.Globalization;
using System.Text;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The ids Add several's rows get: a pin keeps its id, a new one gets the one the server derives from its name, so the write can tell which waiting key paths it made.</summary>
#pragma warning disable MA0182 // Q7b: the Add several view model uses it
internal static class PinSeveralIds
{
    private const int MaxLength = 64;
    private const string Concepts = ".concepts.";

    /// <summary>The id the server derives from a pin's name (lower-case ASCII words joined by '-' or '.', at most 64 characters).</summary>
    /// <param name="name">The pin's name.</param>
    /// <returns>The id, or empty when the name has no Latin letter or digit (the server then derives a hash this client can't).</returns>
    internal static string Slug(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var slug = new StringBuilder();
        foreach (var c in name.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(c);
            if (lower is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.')
            {
                _ = slug.Append(lower);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                _ = slug.Append('-');
            }
        }

        var cleaned = slug.ToString().Trim('-', '.');
        return cleaned.Length > MaxLength ? cleaned[..MaxLength].TrimEnd('-', '.') : cleaned;
    }

    /// <summary>Says whether a waiting key path is one this write made.</summary>
    /// <param name="path">The key path ("index.concepts.billing", "repos.web.index.concepts.billing").</param>
    /// <param name="kept">The ids of the pins the write updates.</param>
    /// <param name="derived">The ids the server derives for the write's new pins; it adds "-2", "-3" when an id is taken.</param>
    /// <returns>True when the path is one of the written pins'.</returns>
    internal static bool Mine(string path, IReadOnlyCollection<string> kept, IReadOnlyCollection<string> derived)
    {
        ArgumentNullException.ThrowIfNull(path);
        var at = path.LastIndexOf(Concepts, StringComparison.Ordinal);
        if (at < 0)
        {
            return false;
        }

        var id = path[(at + Concepts.Length)..];
        return kept.Contains(id, StringComparer.Ordinal) || derived.Any(d => string.Equals(id, d, StringComparison.Ordinal) || IsNumbered(id, d));
    }

    private static bool IsNumbered(string id, string derived) =>
        id.StartsWith(derived + "-", StringComparison.Ordinal) && id.Length > derived.Length + 1 && id[(derived.Length + 1)..].All(char.IsAsciiDigit);
}
#pragma warning restore MA0182
