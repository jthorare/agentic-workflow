using System.Text;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The files under the repo's recipes/ directory (spec §12: the real recipes, the dev recipes, their evals and the
/// dev profile), embedded in this assembly so unit tests read them without file I/O.
/// </summary>
public static class ShippedRecipes
{
    private const string Prefix = "shipped/";

    /// <summary>Every embedded path, relative to recipes/, for example 'sindri/workflow.item.yaml'.</summary>
    public static IReadOnlyList<string> Paths { get; } =
        [.. typeof(ShippedRecipes).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal))
            .Select(n => n[Prefix.Length..])
            .Order(StringComparer.Ordinal)];

    /// <summary>The recipe files: every embedded '.yaml' but the dev profile.</summary>
    public static IReadOnlyList<string> Recipes { get; } =
        [.. Paths.Where(p => p.EndsWith(".yaml", StringComparison.Ordinal) && !p.EndsWith("/profile.yaml", StringComparison.Ordinal))];

    /// <summary>The eval files.</summary>
    public static IReadOnlyList<string> Evals { get; } = [.. Paths.Where(p => p.EndsWith(".jsonl", StringComparison.Ordinal))];

    /// <summary>The text of the file at <paramref name="path"/>, relative to recipes/.</summary>
    /// <param name="path">For example 'sindri/workflow.step.yaml'.</param>
    /// <returns>The file's text.</returns>
    public static string Read(string path)
    {
        using var stream = typeof(ShippedRecipes).Assembly.GetManifestResourceStream(Prefix + path)
            ?? throw new InvalidOperationException($"No shipped recipe file '{path}' is embedded (recipes/{path}).");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
