using System.Text;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The recipe fixtures under Workflow/Recipes, embedded in this assembly so unit tests read them without file I/O.</summary>
public static class RecipeFixtures
{
    /// <summary>The text of the fixture at <paramref name="path"/>, relative to Workflow/Recipes.</summary>
    /// <param name="path">For example 'invalid/RCP-REACH-001.yaml'.</param>
    /// <returns>The fixture's text.</returns>
    public static string Read(string path)
    {
        using var stream = typeof(RecipeFixtures).Assembly.GetManifestResourceStream("recipes/" + path)
            ?? throw new InvalidOperationException($"No recipe fixture '{path}' is embedded (Workflow/Recipes/{path}).");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
