namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): a run's Step attempts.</summary>
internal static partial class Labels
{

    /// <summary>A served id or state as words: separators become spaces, first letter capital.</summary>
    /// <param name="served">The served word.</param>
    /// <returns>The words.</returns>
    internal static string ReadableWord(string served)
    {
        var spaced = served.Replace('-', ' ').Replace('_', ' ').Replace('.', ' ').Trim();
        return spaced.Length == 0 ? served : string.Concat(char.ToUpperInvariant(spaced[0]).ToString(), spaced.AsSpan(1));
    }

}
