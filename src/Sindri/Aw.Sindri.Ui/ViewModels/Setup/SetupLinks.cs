namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>Finds the web address a step's served words carry, so the row can offer it as a link instead of plain text.</summary>
internal static class SetupLinks
{
    /// <summary>The first http or https address in <paramref name="text"/>, without the sentence punctuation after it; null when there is none.</summary>
    /// <param name="text">The served words.</param>
    /// <returns>The address, or null.</returns>
    internal static Uri? UrlIn(string text) => throw new NotSupportedException();
}
