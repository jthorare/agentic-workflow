namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a block hands to the nodes after it: its fields and the outputs an edge may leave by.</summary>
/// <param name="Fields">Field name to type: 'string', 'number', 'boolean', 'object' or 'array'; a trailing '?' marks a field that may be missing.</param>
/// <param name="Branches">The outputs an edge may leave by; null means just 'out'. A 'condition' has 'true' and 'false'.</param>
public sealed record BlockOutputs(IReadOnlyDictionary<string, string> Fields, IReadOnlyList<string>? Branches = null)
{
    /// <summary>The outputs an edge may leave by.</summary>
    public IReadOnlyList<string> BranchNames => Branches ?? ["out"];
}
