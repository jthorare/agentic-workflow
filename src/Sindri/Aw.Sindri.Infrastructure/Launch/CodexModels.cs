using Aw.Kernel.Json;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

#pragma warning disable MA0182 // until the launch probe fills TargetFacts.Models with it (M-3, after M-1's types)
/// <summary>Reads the output of 'codex debug models' (spec §3): keeps the entries whose visibility is 'list', in the order the CLI printed them.</summary>
internal static class CodexModels
{
    /// <summary>Parses the JSON.</summary>
    /// <param name="json">The command's stdout.</param>
    /// <returns>The listed models; empty when the text isn't the catalog JSON (the probe then shows 'no-model-list').</returns>
    public static IReadOnlyList<ModelFacts> Read(string json)
    {
        var parsed = AwJson.Deserialize<CodexModelsDocument>(System.Text.Encoding.UTF8.GetBytes(json), LauncherConfigFile.Json);
        return parsed.IsError || parsed.Value.Models is not { } rows
            ? []
            : Place([.. rows.Where(Listed).Select(Model).OfType<(ModelFacts Model, int? Priority)>()]);
    }

    /// <summary>Gives each model its place from cheapest (0): a larger priority number is cheaper, and a '-mini' goes just before its sibling. A model with no priority keeps no place.</summary>
    private static List<ModelFacts> Place(List<(ModelFacts Model, int? Priority)> listed)
    {
        var ranked = listed.Where(static m => m.Priority is not null).OrderByDescending(static m => m.Priority).Select(static m => m.Model.Slug).ToList();
        foreach (var mini in ranked.Where(static slug => slug.EndsWith("-mini", StringComparison.Ordinal)).ToList())
        {
            var sibling = ranked.IndexOf(mini[..^"-mini".Length]);
            if (sibling >= 0 && sibling < ranked.IndexOf(mini))
            {
                _ = ranked.Remove(mini);
                ranked.Insert(sibling, mini);
            }
        }

        return [.. listed.Select(m => ranked.IndexOf(m.Model.Slug) is var at and >= 0 ? m.Model with { Order = at } : m.Model)];
    }

    private static bool Listed(CodexModelRow row) => string.Equals(row.Visibility, "list", StringComparison.Ordinal);

    private static (ModelFacts Model, int? Priority)? Model(CodexModelRow row) =>
        row.Slug is { Length: > 0 } slug ? (new ModelFacts(slug, row.DisplayName ?? slug, ModelSource.Discovered, Order: null), row.Priority) : null;
}
#pragma warning restore MA0182
