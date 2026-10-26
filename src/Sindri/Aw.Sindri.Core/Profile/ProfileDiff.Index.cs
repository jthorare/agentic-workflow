using System.Globalization;
using System.Text.Json;

namespace Aw.Sindri.Core.Profile;

/// <summary>The paths of the keys the hash covers beyond the explicit lists: the 'index:' section, 'setup:' and 'context:' (#1147).</summary>
public static partial class ProfileDiff
{
    /// <summary>
    /// The 'index:' keys at <paramref name="prefix"/> (the profile's, or a repo's): every one feeds the hash, so each key the profile sets has a path.
    /// A role or a concept has a path of its own, so adding one shows by name.
    /// </summary>
    private static IEnumerable<(string Key, string Value)> IndexKeys(string prefix, IndexSettings? index) =>
        index is null
            ? []
            : IndexLimits(prefix, index).Concat(IndexLayers(prefix, index)).Concat(IndexLists(prefix, index)).Concat(IndexRoles(prefix, index)).Concat(IndexConcepts(prefix, index));

    private static IEnumerable<(string Key, string Value)> IndexLimits(string prefix, IndexSettings index) =>
        Present(
            ($"{prefix}.maxFileKB", Number(index.MaxFileKB)),
            ($"{prefix}.maxTotalMB", Number(index.MaxTotalMB)));

    private static IEnumerable<(string Key, string Value)> IndexLayers(string prefix, IndexSettings index) =>
        Present(
            ($"{prefix}.embeddings.enabled", index.Embeddings?.Enabled?.ToString()),
            ($"{prefix}.embeddings.url", index.Embeddings?.Url),
            ($"{prefix}.embeddings.model", index.Embeddings?.Model),
            ($"{prefix}.graph.enabled", index.Graph?.Enabled?.ToString()),
            ($"{prefix}.graph.maxMB", Number(index.Graph?.MaxMB)));

    private static IEnumerable<(string Key, string Value)> IndexLists(string prefix, IndexSettings index) =>
        Present(
            ($"{prefix}.denyPaths", Joined(index.DenyPaths)),
            ($"{prefix}.utilityGlobs", Joined(index.UtilityGlobs)),
            ($"{prefix}.ignore", Joined(index.Ignore)),
            ($"{prefix}.demote", Joined(index.Demote)));

    private static IEnumerable<(string Key, string Value)> IndexRoles(string prefix, IndexSettings index) =>
        (index.Roles ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)).Select(role => ($"{prefix}.roles.{role.Key}", string.Join(", ", role.Value)));

    private static IEnumerable<(string Key, string Value)> IndexConcepts(string prefix, IndexSettings index) =>
        (index.Concepts ?? []).Select((concept, position) =>
            ($"{prefix}.concepts.{concept.Id?.Value ?? position.ToString(CultureInfo.InvariantCulture)}",
                string.Join(" | ", concept.Name, concept.Summary, Joined(concept.Features), Joined(concept.Paths))));

    /// <summary>The keys that have a value; an absent key has no path, so adding one shows as a change.</summary>
    private static IEnumerable<(string Key, string Value)> Present(params (string Key, string? Value)[] keys) =>
        keys.Where(static k => k.Value is not null).Select(static k => (k.Key, k.Value!));   // why: the nulls are filtered out just above

    /// <summary>The 'setup:' key and the 'context:' section, whose every leaf has a path (they feed the hash as well).</summary>
    private static IEnumerable<(string Key, string Value)> SetupAndContext(SindriProfile p) =>
        Present(("setup.mcp", p.Setup?.Mcp)).Concat(p.Context is { } context ? Leaves("context", context) : []);

    private static IEnumerable<(string Key, string Value)> Leaves(string path, JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().OrderBy(static p => p.Name, StringComparer.Ordinal).SelectMany(p => Leaves($"{path}.{p.Name}", p.Value)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany((item, position) => Leaves(string.Create(CultureInfo.InvariantCulture, $"{path}[{position}]"), item)),
        JsonValueKind.Undefined or JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null or _ => [(path, element.ToString())],
    };
}
