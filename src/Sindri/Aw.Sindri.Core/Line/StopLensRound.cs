using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>One lens round.</summary>
/// <param name="Round">Its number.</param>
/// <param name="Lenses">What each lens reported.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopLensRound(
    int Round,
    IReadOnlyList<StopLensRow> Lenses);
