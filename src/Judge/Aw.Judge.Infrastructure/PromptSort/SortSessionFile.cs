using System.Text.Json.Nodes;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.PromptSort;

namespace Aw.Judge.Infrastructure.PromptSort;

/// <summary>The shape of 'judge/sessions/&lt;sid&gt;.sort.json': {prompts, lastFired, requirements: {uiEvidence?}} (frozen, done-gate.sh reads it).</summary>
internal static class SortSessionFile
{
    /// <summary>The state a file holds; the empty state when it has no numeric 'prompts'.</summary>
    internal static SortSession Read(JsonNode? node)
    {
        if (node is not JsonObject file || Number(file["prompts"]) is not { } prompts)
        {
            return SortSession.Empty;
        }

        var lastFired = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var id in ProviderNames.Scaffolds)
        {
            if (file["lastFired"] is JsonObject fired && Number(fired[id]) is { } at)
            {
                lastFired[id] = at;
            }
        }

        var ui = file["requirements"] is JsonObject requirements && requirements["uiEvidence"] is JsonValue flag && flag.TryGetValue<bool>(out var on) && on;
        return new SortSession(prompts, lastFired, new SortRequirements(ui));
    }

    private static int? Number(JsonNode? node) => node is JsonValue value && value.TryGetValue<double>(out var number) ? (int)number : null;

    /// <summary>The file with the state written into it: every key and every other requirement it already had is kept.</summary>
    internal static JsonObject Merge(JsonObject? existing, SortSession state)
    {
        var file = existing ?? [];
        file["prompts"] = state.Prompts;
        file["lastFired"] = new JsonObject(state.LastFired.Select(static pair => KeyValuePair.Create(pair.Key, (JsonNode?)JsonValue.Create(pair.Value))));
        if (file["requirements"] is not JsonObject requirements)
        {
            requirements = [];
            file["requirements"] = requirements;
        }

        _ = requirements.Remove("uiEvidence");
        if (state.Requirements.UiEvidence)
        {
            requirements["uiEvidence"] = true;
        }

        return file;
    }
}
