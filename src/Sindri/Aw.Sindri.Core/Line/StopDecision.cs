using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>An open decision at stop 2.</summary>
/// <param name="Decision">The decision, for example D3.</param>
/// <param name="Question">What it asks.</param>
/// <param name="Status">open or answered.</param>
/// <param name="Recommended">The recommended option.</param>
/// <param name="Chosen">The option picked, when answered.</param>
/// <param name="Affects">The sections the answer changes.</param>
/// <param name="Cites">What the question comes from.</param>
/// <param name="Options">The options, in the order written.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopDecision(
    string Decision,
    string Question,
    string Status,
    string Recommended,
    string? Chosen,
    IReadOnlyList<string> Affects,
    IReadOnlyList<string> Cites,
    IReadOnlyList<StopOption> Options);
