using System.Globalization;
using System.Text;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The ids Add several's rows get: a pin keeps its id, a new one gets the one the server derives from its name.</summary>
#pragma warning disable MA0182 // Q7b: the Add several view model uses it
internal static class PinSeveralIds
{
    private const int MaxLength = 64;

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
}
#pragma warning restore MA0182
