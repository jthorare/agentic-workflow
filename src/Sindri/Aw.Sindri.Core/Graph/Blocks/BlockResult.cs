using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a node hands on.</summary>
/// <param name="Outputs">The fields later nodes may read, by name.</param>
/// <param name="Branches">The outputs an edge may leave by; every edge on another output is skipped. Null means just 'out'.</param>
public sealed record BlockResult(IReadOnlyDictionary<string, JsonNode?> Outputs, IReadOnlyList<string>? Branches = null)
{
    /// <summary>The branches taken.</summary>
    public IReadOnlyList<string> Taken => Branches ?? ["out"];
}
