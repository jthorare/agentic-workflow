using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>One row of the coverage table.</summary>
/// <param name="Scope">The scope surface.</param>
/// <param name="Key">Its key.</param>
/// <param name="Sections">The sections that cover it.</param>
/// <param name="Excluded">The reason, when it is excluded rather than covered.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopCoverageRow(
    string Scope,
    string Key,
    IReadOnlyList<string> Sections,
    string? Excluded);
