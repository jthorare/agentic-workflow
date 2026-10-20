using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>The plan graph as stacks (columns) and waves (rows).</summary>
/// <param name="Parallel">How many phases a wave runs at once.</param>
/// <param name="MaxStack">The deepest a stack may go.</param>
/// <param name="PrCount">How many pull requests the plan opens.</param>
/// <param name="CriticalPath">The longest chain of phases.</param>
/// <param name="Waves">The phases of each wave.</param>
/// <param name="Phases">The phases in plan order.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopGraph(
    int Parallel,
    int MaxStack,
    int PrCount,
    IReadOnlyList<string> CriticalPath,
    IReadOnlyList<IReadOnlyList<string>> Waves,
    IReadOnlyList<StopPhase> Phases);
