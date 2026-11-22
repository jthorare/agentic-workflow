using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Core.Evolve;

/// <summary>The one normalization every leak layer reads text through, so look-alike spellings can't slip past.</summary>
[SuppressMessage("Meziantou.Analyzer", "MA0182:Internal type is apparently never used", Justification = "A stub until the next commit, where the tier rules and leak layers call it.")]
internal static class PrivacyText
{
    /// <summary>NFKC, format characters (zero-width, bidi) removed, every run of white space folded to one space, trimmed.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The visible form.</returns>
    public static string Visible(string text) => throw new NotSupportedException();

    /// <summary>The visible form lowercased and split into words of letters and digits; everything else separates.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The words, in order.</returns>
    public static IReadOnlyList<string> Words(string text) => throw new NotSupportedException();
}
