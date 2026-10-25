using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>Finds the web address a step's served words carry, so the row can offer it as a link instead of plain text.</summary>
internal static partial class SetupLinks
{
    /// <summary>The first http or https address in <paramref name="text"/>, without the sentence punctuation after it; null when there is none.</summary>
    /// <param name="text">The served words.</param>
    /// <returns>The address, or null.</returns>
    internal static Uri? UrlIn(string text)
    {
        var found = Address().Match(text);
        return found.Success && Uri.TryCreate(found.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')'), UriKind.Absolute, out var url) ? url : null;
    }

    [GeneratedRegex(@"https?://[^\s<>""]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex Address();
}
