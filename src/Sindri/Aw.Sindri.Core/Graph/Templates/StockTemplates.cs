namespace Aw.Sindri.Core.Graph.Templates;

/// <summary>Templates A, B and C of the Workflows page spec. Red stub: the engine lane loads them from the template files.</summary>
public static class StockTemplates
{
    /// <summary>Gets the templates in the order the start screen shows them.</summary>
    public static IReadOnlyList<StockTemplate> All => throw new NotSupportedException("the stock templates are not built yet");

    /// <summary>Finds a template by id.</summary>
    /// <param name="key">'A', 'B' or 'C'.</param>
    /// <returns>The template, or null.</returns>
    public static StockTemplate? Find(string key) => throw new NotSupportedException("the stock templates are not built yet");
}
