using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Host.Registration;

/// <summary>
/// <see cref="IShippedEffects"/> over the recipes embedded in this build (setup-once spec §5.1, §5.3; 'aw-recipes/sindri/*.yaml', staged from 'git archive HEAD' by the publish): the union of the effects their
/// transitions emit, read once. A build that embeds none ships no effect. A file that does not bind is left out; the engine's registration reports it.
/// </summary>
/// <param name="reader">Reads a recipe's text.</param>
internal sealed class EmbeddedShippedEffects(IRecipeReader reader) : IShippedEffects
{
    private const string Prefix = "aw-recipes/sindri/";

    private readonly Lazy<IReadOnlyList<string>> _kinds = new(() => Read(reader), LazyThreadSafetyMode.ExecutionAndPublication);

    public IReadOnlyList<string> Kinds() => _kinds.Value;

    private static IReadOnlyList<string> Read(IRecipeReader reader)
    {
        var assembly = typeof(EmbeddedShippedEffects).Assembly;
        List<Recipe> recipes = [];
        foreach (var name in assembly.GetManifestResourceNames().Where(static n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(".yaml", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            using var text = new StreamReader(stream);
            if (reader.Read(text.ReadToEnd(), name[Prefix.Length..]).Document is { } document)
            {
                recipes.Add(document.Recipe);
            }
        }

        return ShippedEffects.Union(recipes);
    }
}
