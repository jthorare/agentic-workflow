using System.Text.Json.Nodes;
using Aw.Sindri.Core.Judge;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// What the gap judge is shown about one candidate (issue 966, spec 4.5): the brief's text, the acceptance items, the map's surfaces, the gathered entries and the candidate's title, kind,
/// citation and excerpt. Built from named fields only, so nothing of the searcher's argument (its reason, query, anchor, searches or model) can reach the judge.
/// </summary>
internal static class GapJudgeProjection
{
    /// <summary>The ruling for a candidate the change needs and the scope does not cover.</summary>
    internal const string RealGap = "real-gap";

    /// <summary>The ruling for a candidate a map surface or a gathered entry already is.</summary>
    internal const string AlreadyCovered = "already-covered";

    /// <summary>The ruling for a candidate that is real but not part of the change.</summary>
    internal const string NotInScope = "not-in-scope";

    /// <summary>The question.</summary>
    internal const string Question = "A search for what the scope of the change in 'brief' misses found the 'candidate'. Does the change need it, and do the 'map' and the 'gathered' entries not cover it already?";

    /// <summary>The three rulings and when to pick each.</summary>
    internal static IReadOnlyList<ChooseOption> Choices { get; } =
    [
        new(RealGap, "the change in the brief needs the candidate, and no surface in the map and no gathered entry is it"),
        new(AlreadyCovered, "a surface in the map or a gathered entry is the candidate, or the acceptance items already cover it"),
        new(NotInScope, "the candidate is real, but it is not part of the change in the brief"),
    ];

    /// <summary>The facts one call reads, by key, as text: the brief, the acceptance items, the map's surfaces, the gathered entries and the candidate.</summary>
    /// <param name="brief">The brief fact.</param>
    /// <param name="acceptance">The acceptance fact.</param>
    /// <param name="scopeMap">The scope map fact.</param>
    /// <param name="lenses">The lenses fact.</param>
    /// <param name="candidate">One candidate of the searcher's fact.</param>
    /// <returns>The facts.</returns>
    internal static IReadOnlyDictionary<string, string> Facts(JsonNode? brief, JsonNode? acceptance, JsonNode? scopeMap, JsonNode? lenses, JsonNode? candidate) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["brief"] = BriefText(brief),
            ["acceptance"] = Lines(ScopeJson.Items(acceptance, "items").Select(static i => Join(": ", ScopeJson.Str(i, "id"), ScopeJson.Str(i, "text")))),
            ["map"] = Lines(ScopeJson.Items(scopeMap, "surfaces").Select(static s => Join(" | ", ScopeJson.Str(s, "id"), ScopeJson.Str(s, "key"), ScopeJson.Str(s, "title"), ScopeJson.Str(s, "kind")))),
            ["gathered"] = Lines(Gathered(lenses)),
            ["candidate"] = Lines(
            [
                "title: " + ScopeJson.Str(candidate, "title"),
                "kind: " + ScopeJson.Str(candidate, "kind"),
                "citation: " + ScopeJson.Str(candidate, "citation"),
                "excerpt: " + ScopeJson.Str(candidate, "excerpt"),
            ]),
        };

    // The brief fact is {text, refs, subject}: its text only, never its refs (which carry the source's trust and key).
    private static string BriefText(JsonNode? brief) => brief is JsonValue value && value.TryGetValue<string>(out var plain) ? plain : ScopeJson.Str(brief, "text") ?? string.Empty;

    // Every entry of every lens that holds any: a node by key, kind and label, an item (a record, decision, commit or symbol) by kind and label.
    private static IEnumerable<string> Gathered(JsonNode? lenses) =>
        lenses is JsonObject all
            ? all.Select(static p => p.Value).SelectMany(static lens =>
                ScopeJson.Items(lens, "nodes").Select(static n => Join(" | ", ScopeJson.Str(n, "key"), ScopeJson.Str(n, "kind"), ScopeJson.Str(n, "label")))
                    .Concat(ScopeJson.Items(lens, "items").Select(static i => Join(" | ", ScopeJson.Str(i, "kind"), ScopeJson.Str(i, "label")))))
            : [];

    private static string Join(string separator, params string?[] parts) => string.Join(separator, parts.Select(static p => p ?? string.Empty));

    private static string Lines(IEnumerable<string> lines) => string.Join('\n', lines);
}
