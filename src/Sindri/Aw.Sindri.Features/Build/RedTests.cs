using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Build;

/// <summary>
/// The red tests as 'fact:redTests' holds them (line spec A.7): <c language="text">{ commit?, items: { "&lt;item&gt;": { tests: [ids] } | { appOnly: signal } } }</c>. 'commit' is the red commit; an 'appOnly' item has no
/// test to run. The prepare Step seeds the fact as <c language="text">{}</c>, which holds no item.
/// </summary>
/// <param name="Commit">The red commit, or null when the builder named none.</param>
/// <param name="Items">The items that have tests, in the fact's order.</param>
internal sealed record RedTests(string? Commit, IReadOnlyList<RedItem> Items)
{
    internal static RedTests From(JsonNode? fact)
    {
        var items = fact is JsonObject obj && obj["items"] is JsonObject map
            ? map.Select(static e => new RedItem(e.Key, Tests(e.Value))).Where(static i => i.Tests.Count > 0).ToList()
            : [];
        return new RedTests(StepFacts.Field(fact, "commit"), items);
    }

    private static List<string> Tests(JsonNode? item) =>
        item is JsonObject obj && obj["tests"] is JsonArray listed
            ? [.. listed.Select(static t => t is JsonValue v && v.TryGetValue<string>(out var text) ? text : null).OfType<string>().Where(static t => !string.IsNullOrWhiteSpace(t))]
            : [];
}
