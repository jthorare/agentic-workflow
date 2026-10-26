namespace Aw.Sindri.Features.Scoping;

/// <summary>The files a scope packet holds (scoping spec §8.3): the only names API9 serves.</summary>
internal static class ScopePacketFiles
{
    /// <summary>The names, in the packet's order.</summary>
    internal static IReadOnlyList<string> Names { get; } =
    [
        "scope-map.json", "acceptance.json", "coverage.json", "lenses.json", "relevance.json", "size.json", "questions.json",
        "answers.jsonl", "challenge.json", "gap-search.json", "rounds.jsonl", "runs.json", "sources.json", "brief.md",
    ];

    /// <summary>'json', 'jsonl' or 'md' by the extension.</summary>
    internal static string Format(string name) => name.EndsWith(".jsonl", StringComparison.Ordinal) ? "jsonl" : name.EndsWith(".md", StringComparison.Ordinal) ? "md" : "json";
}
